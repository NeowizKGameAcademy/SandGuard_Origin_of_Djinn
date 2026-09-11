using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace SandGuard.Player.Editor
{
    // Isolated preview objects: never samples or edits the scene's player.
    public sealed class PlayerMotionComparisonWindow : EditorWindow
    {
        const string Request = "Library/PlayerMotionComparison.request";
        PreviewRenderUtility preview;
        readonly GameObject[] models = new GameObject[2];
        readonly PlayableGraph[] graphs = new PlayableGraph[2];
        readonly AnimationClipPlayable[] motions = new AnimationClipPlayable[2];
        readonly AnimationClip[] clips = new AnimationClip[2];
        static readonly string[] Characters = { "Player", "Swordsman", "Assassin", "ShieldGuard", "HammerBrute", "Chief" };
        static readonly string[] Roles = { "Idle", "Move", "Attack", "Death" };
        [SerializeField] int character;
        int gait, angle, leftRole, rightRole = 1;
        string[] labels = new string[2];
        bool playing = true, feetOnly, footIK;
        float phase, rate = 1;
        double lastTime;
        string error;

        [MenuItem("SandGuard/Animation Library/Compare Character Motions")]
        [MenuItem("SandGuard/Player/Compare Original and Magic Motions")]
        public static void Open()
        {
            var window = GetWindow<PlayerMotionComparisonWindow>("캐릭터 모션 비교");
            window.minSize = new Vector2(680, 480);
            window.Show();
        }

        [InitializeOnLoadMethod]
        static void ConsumeRequest()
        {
            if (!File.Exists(Request)) return;
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                { ConsumeRequest(); return; }
                File.Delete(Request);
                try { Validate(); Open(); var window = GetWindow<PlayerMotionComparisonWindow>(); window.character = 1; window.feetOnly = true; window.Release(); window.Repaint(); }
                catch (Exception ex) { Debug.LogException(ex); File.WriteAllText("Logs/player-motion-comparison-status.txt", ex.ToString()); }
            };
        }

        void OnEnable()
        {
            lastTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
        }

        void OnDisable()
        {
            EditorApplication.update -= Tick;
            Release();
        }

        void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            if (playing && clips[0] != null)
                phase = Mathf.Repeat(phase + (float)Math.Min(now - lastTime, .1) * rate / clips[0].length, 1);
            lastTime = now;
            if (playing) Repaint();
        }

        void BuildPreview()
        {
            Release();
            preview = new PreviewRenderUtility();
            preview.camera.fieldOfView = 32;
            preview.camera.nearClipPlane = .01f;
            preview.camera.farClipPlane = 100;
            preview.camera.clearFlags = CameraClearFlags.Color;
            preview.camera.backgroundColor = new Color(.12f, .14f, .17f);
            preview.ambientColor = new Color(.5f, .5f, .5f);
            preview.lights[0].intensity = 1.2f;
            preview.lights[0].transform.rotation = Quaternion.Euler(35, -30, 0);
            preview.lights[1].intensity = .7f;
            string name = Characters[character];
            string folder = character == 0 ? ProtagonistArtBuilder.Art : "Assets/Enemy/Art/Characters/" + name;
            string modelPath = folder + "/" + (character == 0 ? "Protagonist" : name + "_Rig") + ".fbx";
            string materialPath = folder + "/" + (character == 0 ? "Protagonist" : name + "_Combat") + ".mat";
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (source == null) throw new InvalidOperationException(name + " 모델을 찾지 못했습니다.");
            if (character == 0)
            {
                clips[0] = ProtagonistArtBuilder.Clip(gait == 0 ? "Walk" : "Run");
                clips[1] = PlayerAnimationPackBuilder.Clip(gait == 0 ? "WalkForward" : "RunForward");
                labels = new[] { "직접 받은 모션 / Player", "새 다운로드 / 현재 적용" };
            }
            else
            {
                clips[0] = LoadClip(folder + "/" + name + "_" + Roles[leftRole] + ".fbx");
                clips[1] = LoadClip(folder + "/" + name + "_" + Roles[rightRole] + ".fbx");
                labels = new[] { name + " / " + Roles[leftRole], name + " / " + Roles[rightRole] };
            }
            for (int i = 0; i < 2; i++)
            {
                models[i] = Instantiate(source);
                models[i].hideFlags = HideFlags.HideAndDontSave;
                preview.AddSingleGO(models[i]);
                foreach (var renderer in models[i].GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                    renderer.updateWhenOffscreen = true;
                }
                var animator = models[i].GetComponent<Animator>();
                animator.runtimeAnimatorController = null;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                graphs[i] = PlayableGraph.Create("Player Motion Comparison " + i);
                graphs[i].SetTimeUpdateMode(DirectorUpdateMode.Manual);
                motions[i] = AnimationClipPlayable.Create(graphs[i], clips[i]);
                motions[i].SetApplyPlayableIK(false);
                var output = AnimationPlayableOutput.Create(graphs[i], "Preview", animator);
                output.SetSourcePlayable(motions[i]);
                graphs[i].Play();
            }
            Sample();
        }

        void Sample()
        {
            for (int i = 0; i < 2; i++)
            {
                motions[i].SetApplyFootIK(footIK);
                motions[i].SetTime(phase * clips[i].length);
                graphs[i].Evaluate(0);
                models[i].transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            }
        }

        void Release()
        {
            for (int i = 0; i < 2; i++)
            {
                if (graphs[i].IsValid()) graphs[i].Destroy();
                if (models[i] != null) DestroyImmediate(models[i]);
                models[i] = null;
                clips[i] = null;
            }
            preview?.Cleanup();
            preview = null;
        }

        void OnGUI()
        {
            EditorGUI.BeginChangeCheck();
            character = EditorGUILayout.Popup("캐릭터", character, Characters);
            if (EditorGUI.EndChangeCheck()) { phase = 0; Release(); error = null; }
            EditorGUILayout.HelpBox(character == 0
                ? "왼쪽: 직접 받은 모션 / 오른쪽: 캐릭터별 새 다운로드. 동일한 Player 모델에 적용한 결과입니다."
                : "동일한 Enemy 모델에서 두 모션을 비교합니다. 기본은 왼쪽 대기(Idle), 오른쪽 이동(Move)입니다.", MessageType.Info);
            EditorGUILayout.LabelField("단일 모션 비교 · 방향 혼합과 손·장비 보정 제외 · 실제 게임 설정 유지", EditorStyles.miniLabel);
            EditorGUI.BeginChangeCheck();
            if (character == 0) gait = GUILayout.Toolbar(gait, new[] { "걷기", "달리기" });
            else
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    leftRole = EditorGUILayout.Popup("왼쪽 모션", leftRole, Roles);
                    rightRole = EditorGUILayout.Popup("오른쪽 모션", rightRole, Roles);
                }
            }
            if (EditorGUI.EndChangeCheck()) { phase = 0; Release(); error = null; }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(playing ? "일시정지" : "재생", GUILayout.Width(85))) playing = !playing;
                if (GUILayout.Button("처음", GUILayout.Width(55))) phase = 0;
                if (GUILayout.Button("한 칸 ▶", GUILayout.Width(80))) { playing = false; phase = Mathf.Repeat(phase + 1f / 120, 1); }
                rate = EditorGUILayout.Slider("재생 속도", rate, .1f, 1.5f);
            }
            EditorGUI.BeginChangeCheck();
            float nextPhase = EditorGUILayout.Slider("동작 진행률", phase, 0, 1);
            if (EditorGUI.EndChangeCheck()) { phase = nextPhase; playing = false; }
            using (new EditorGUILayout.HorizontalScope())
            {
                angle = GUILayout.Toolbar(angle, new[] { "정면", "측면", "후면" });
                feetOnly = GUILayout.Toggle(feetOnly, "발목 확대", GUILayout.Width(100));
                footIK = GUILayout.Toggle(footIK, "Foot IK (양쪽)", GUILayout.Width(125));
            }
            EditorGUILayout.LabelField("동작 진행률을 맞춰 재생하므로 두 클립의 실제 초당 재생 속도는 다를 수 있습니다.", EditorStyles.miniLabel);
            try
            {
                if (preview == null && error == null) BuildPreview();
                if (error != null) { EditorGUILayout.HelpBox(error, MessageType.Error); return; }
                Sample();
                var area = GUILayoutUtility.GetRect(100, 10000, 180, 10000);
                float half = area.width / 2;
                DrawModel(new Rect(area.x, area.y, half - 2, area.height), 0);
                DrawModel(new Rect(area.x + half + 2, area.y, half - 2, area.height), 1);
            }
            catch (Exception ex) { error = ex.Message; Release(); Debug.LogException(ex); }
        }

        void DrawModel(Rect rect, int index)
        {
            if (Event.current.type != EventType.Repaint) return;
            for (int i = 0; i < 2; i++)
                foreach (var renderer in models[i].GetComponentsInChildren<Renderer>()) renderer.enabled = i == index;
            var target = new Vector3(0, feetOnly ? .32f : .95f, 0);
            var direction = angle == 0 ? Vector3.forward : angle == 1 ? Vector3.right : Vector3.back;
            preview.camera.transform.position = target + direction * (feetOnly ? 1.65f : 4.5f) + Vector3.up * .1f;
            preview.camera.transform.LookAt(target);
            preview.BeginPreview(rect, GUIStyle.none);
            preview.Render(true);
            GUI.DrawTexture(rect, preview.EndPreview(), ScaleMode.StretchToFill, false);
            GUI.Label(new Rect(rect.x + 10, rect.y + 8, rect.width - 20, 45),
                labels[index] + "\n" + clips[index].name + "  (" + clips[index].length.ToString("F2") + "초)", EditorStyles.whiteLabel);
            for (int i = 0; i < 2; i++)
                foreach (var renderer in models[i].GetComponentsInChildren<Renderer>()) renderer.enabled = true;
        }

        static AnimationClip LoadClip(string path) => AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));

        public static void Validate()
        {
            var window = CreateInstance<PlayerMotionComparisonWindow>();
            try
            {
                for (int who = 0; who < Characters.Length; who++)
                for (int gait = 0; gait < 2; gait++)
                {
                    window.character = who;
                    window.leftRole = gait * 2; window.rightRole = gait * 2 + 1;
                    window.gait = gait;
                    window.BuildPreview();
                    for (int frame = 0; frame < 12; frame++)
                    {
                        window.phase = frame / 12f;
                        window.Sample();
                        for (int i = 0; i < 2; i++)
                        {
                            var animator = window.models[i].GetComponent<Animator>();
                            if (!animator.isHuman || !window.clips[i].humanMotion) throw new InvalidOperationException("Invalid Humanoid preview");
                            var foot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                            if (foot == null || !float.IsFinite(foot.position.y)) throw new InvalidOperationException("Invalid foot pose");
                        }
                    }
                }
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/player-motion-comparison-status.txt", "PASS: Player Walk/Run + all 5 Enemies Idle/Move/Attack/Death; 12 poses per clip. Gameplay assets unchanged.");
            }
            finally { DestroyImmediate(window); }
        }
    }
}

