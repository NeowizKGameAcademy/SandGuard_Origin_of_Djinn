using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SandGuard.Enemy.Editor
{
    /// <summary>Mixamo 리깅 몸체 + 선정 클립 + 장비를 전투용 외형 프리팹으로 묶고 Enemy 프리팹에 연결한다.</summary>
    /// <remarks>
    /// 원본은 <c>Docs/model-art</c>에 두고 쓰는 것만 <c>Assets/Enemy/Art/Characters/&lt;이름&gt;</c>로 복사한다.
    /// 정적 미리보기(<see cref="EnemyArtPreviewBuilder"/>)의 키·장비 배율을 그대로 쓰고, 장비 방향은 손 본의 실제 기하(손가락·엄지 방향)로 계산한다.
    /// </remarks>
    public static class EnemyCombatArtBuilder
    {
        const string Art = "Assets/Enemy/Art/Characters";
        const string Equipment = "Assets/Enemy/Art/Equipment";
        const string Generated = "Assets/Enemy/Generated";
        const string Selection = "Docs/model-art/animation-selection-v1";
        const string Report = "Docs/model-art/enemy-combat-art-v1";
        public const string BasePrefab = Generated + "/Enemy.prefab";

        sealed class Spec
        {
            public string Name, BodyFolder, RigFile, AttackClip = "Attack";
            public float Height, MoveSpeed = 3.5f, AttackSpeed = 1f, Windup, Interval = 1.2f;
            public float? MoveTimeScale;
            public float? AttackRange;
            /// <summary>어그로 순위(플레이어, 미니언, 타워, 코어). 작을수록 먼저 노린다. null이면 기본 프리팹 값을 따른다.</summary>
            public (int player, int minion, int tower, int core)? Priorities;
            public (string asset, HumanBodyBones bone, float scale)[] Gear;
        }

        // 키·장비 배율은 EnemyArtPreviewBuilder에서 확정한 값. 타격 시점(Windup)은 Blender로 잰 손 최대 전진 프레임(문서 §3.3).
        static readonly Spec[] Specs =
        {
            new Spec { Name = "Swordsman", BodyFolder = "bandit-minion", RigFile = "Docs/model-art/Pro Sword and Shield Pack/bandit1.fbx", Height = 1.75f, Windup = .5f,
                Gear = new[] { ("ShortSword", HumanBodyBones.RightHand, .65f), ("RoundShield", HumanBodyBones.LeftHand, .9f) } },
            new Spec { Name = "Assassin", BodyFolder = "bandit-ninja", Height = 1.68f, AttackClip = "AttackQuick", AttackSpeed = 2f, Windup = .3f,
                Gear = new[] { ("AssassinDagger", HumanBodyBones.RightHand, .63f), ("AssassinDagger", HumanBodyBones.LeftHand, .63f) } },
            new Spec { Name = "ShieldGuard", BodyFolder = "bandit-shielder", Height = 1.84f, MoveSpeed = 2f, Windup = .5f,
                Gear = new[] { ("ShortSword", HumanBodyBones.RightHand, .72f), ("TowerShield", HumanBodyBones.LeftHand, 1f) } },
            new Spec { Name = "HammerBrute", BodyFolder = "bandit-hammerer", Height = 1.94f, MoveTimeScale = 1f, AttackRange = 2.3f, AttackSpeed = 2.836364f, Windup = .93f, Interval = 1.9f,
                Gear = new[] { ("Warhammer", HumanBodyBones.RightHand, 1f) } },
            new Spec { Name = "Chief", BodyFolder = "bandit-leader", Height = 2.04f, MoveSpeed = 2.2f, AttackRange = 3f, Windup = .6f, Interval = 1.6f,
                Gear = new[] { ("ChiefScimitar", HumanBodyBones.RightHand, 1f), ("ChiefCape", HumanBodyBones.Chest, 2.04f / 1.8f) } },
        };

        public static string VisualPath(string name) => Art + "/" + name + "/" + name + "_CombatVisual.prefab";
        public static string VariantPath(string name) => Generated + "/Enemy_" + name + ".prefab";

        [MenuItem("SandGuard/Enemy/Connect Combat Art")]
        public static void Build()
        {
            Directory.CreateDirectory(Report);
            var report = new List<string>();
            foreach (var spec in Specs) BuildCharacter(spec, report);
            ConnectPrefabs(report);
            PlaceShowcase();
            AssetDatabase.SaveAssets();
            File.WriteAllLines(Report + "/build-report.txt", report);
            Debug.Log("ENEMY_COMBAT_ART_COMPLETE\n" + string.Join("\n", report));
        }

        static void BuildCharacter(Spec spec, List<string> report)
        {
            string folder = Art + "/" + spec.Name, docs = "Docs/model-art/" + spec.BodyFolder;
            string existingRig = folder + "/" + spec.Name + "_Rig.fbx";
            string rigSource = File.Exists(existingRig) ? existingRig : spec.RigFile ?? DownloadedMotion(spec, "Idle");
            string textureSource = Directory.GetFiles(docs, "*.jpg", SearchOption.AllDirectories).First(p => p.Replace('\\', '/').Contains(".fbm/"));
            string rig = folder + "/" + spec.Name + "_Rig.fbx", texture = folder + "/" + spec.Name + "_BaseColor.jpg";
            if (rigSource != rig) File.Copy(rigSource, rig, true);
            File.Copy(textureSource, texture, true);
            var clips = new Dictionary<string, string>();
            foreach (var (role, file) in new[] { ("Idle", "Idle"), ("Move", "Move"), ("Attack", spec.AttackClip), ("Death", "Death") })
            {
                string target = folder + "/" + spec.Name + "_" + role + ".fbx";
                File.Copy(DownloadedMotion(spec, role), target, true);
                clips[role] = target;
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            var ti = (TextureImporter)AssetImporter.GetAtPath(texture);
            ti.textureType = TextureImporterType.Default; ti.sRGBTexture = true; ti.maxTextureSize = 2048;
            ti.mipmapEnabled = true; ti.textureCompression = TextureImporterCompression.CompressedHQ; ti.SaveAndReimport();
            var material = AssetDatabase.LoadAssetAtPath<Material>(folder + "/" + spec.Name + "_Combat.mat");
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, folder + "/" + spec.Name + "_Combat.mat"); }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texture));
            material.SetColor("_BaseColor", Color.white); material.SetFloat("_Metallic", 0); material.SetFloat("_Smoothness", .15f);
            EditorUtility.SetDirty(material);

            // Mixamo 몸체는 원본 Tripo 높이(약 1)를 따르므로, 먼저 1배로 들여와 최종과 같은 조건(T포즈 인스턴스)에서 실측한 뒤 목표 키로 맞춘다.
            ImportBody(rig, 1f, material);
            float measured = MeasureHeight(rig);
            if (measured < .1f) throw new InvalidOperationException(spec.Name + ": body height could not be measured.");
            ImportBody(rig, spec.Height / measured, material);
            var avatar = AssetDatabase.LoadAllAssetsAtPath(rig).OfType<Avatar>().FirstOrDefault();
            if (avatar == null || !avatar.isValid || !avatar.isHuman) throw new InvalidOperationException(spec.Name + ": Humanoid avatar is invalid.");
            foreach (var pair in clips) ImportClip(pair.Value, pair.Key, loop: pair.Key == "Idle" || pair.Key == "Move");

            var root = new GameObject(spec.Name + "_CombatVisual");
            try
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(rig));
                model.name = "Character"; model.transform.SetParent(root.transform, false);
                var animator = model.GetComponent<Animator>();
                foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>())
                { renderer.sharedMaterial = material; renderer.updateWhenOffscreen = true; }
                animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.Rebind(); animator.Update(0f);
                float stride = Stride(clips["Move"], animator);
                // 실측 보폭으로 발걸음이 너무 느려지는 클립은 캐릭터별 재생 배속을 우선한다.
                float timeScale = spec.MoveTimeScale ?? (stride < .2f ? 1f : Mathf.Clamp(spec.MoveSpeed / stride, .5f, 2.5f));
                animator.runtimeAnimatorController = Controller(spec, folder, clips, timeScale);
                root.AddComponent<EnemyVisualBindings>().animator = animator;

                // 근육 0 자세는 T포즈가 아니다(무릎·팔이 굽는다). 장비 방향은 손 본 기하로 자세와 무관하게 구하고, 등 소켓은 바인드 포즈(T포즈)에서 잡는다.
                Bounds bounds = BoundsOf(model);
                if (Mathf.Abs(bounds.size.y - spec.Height) > spec.Height * .03f)
                    throw new InvalidOperationException(spec.Name + ": height " + bounds.size.y.ToString("F3") + " does not match target " + spec.Height);
                foreach (var (asset, bone, scale) in spec.Gear) Equip(animator, asset, bone, scale, bounds, report, spec.Name);
                EnemyEquipmentFitBuilder.Configure(animator, spec.Name);
                PrefabUtility.SaveAsPrefabAsset(root, VisualPath(spec.Name));
                float death = Clip(clips["Death"]).length, attack = Clip(clips["Attack"]).length / spec.AttackSpeed;
                report.Add(spec.Name + ": height=" + bounds.size.y.ToString("F2") + "m (target " + spec.Height + ") stride=" + stride.ToString("F2")
                    + "m/s moveTimeScale=" + timeScale.ToString("F2") + " attack=" + attack.ToString("F2") + "s (windup " + spec.Windup + ", interval " + spec.Interval
                    + ") death=" + death.ToString("F2") + "s bones=" + animator.avatar.humanDescription.human.Length);
            }
            finally { Object.DestroyImmediate(root); }
        }

        static float MeasureHeight(string rig)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(rig));
            try
            {
                var animator = instance.GetComponent<Animator>();
                // 애니메이션을 들여오지 않았으므로 인스턴스는 Mixamo 바인드 포즈(T포즈)에 선다. 최종 프리팹도 같은 자세로 저장된다.
                animator.Rebind(); animator.Update(0f);
                return BoundsOf(instance).size.y;
            }
            finally { Object.DestroyImmediate(instance); }
        }

        static string DownloadedMotion(Spec spec, string role)
        {
            string[] names = spec.Name switch
            {
                "Swordsman" => new[] { "Sword And Shield Idle", "Sword And Shield Run", "Sword And Shield Slash", "Sword And Shield Death" },
                "Assassin" => new[] { "Knife Idle", "Run", "Standing Torch Melee Attack Stab", "Standing React Death Left" },
                "ShieldGuard" => new[] { "Sword And Shield Block Idle", "Sword And Shield Walk", "Sword And Shield Slash", "Sword And Shield Death" },
                "HammerBrute" => new[] { "Great Sword Idle", "Great Sword Walk", "Heavy Weapon Swing", "Two Handed Sword Death" },
                "Chief" => new[] { "Sword And Shield Idle", "Sword And Shield Walk", "Sword And Shield Slash", "Standing React Death Backward" },
                _ => throw new InvalidOperationException(spec.Name)
            };
            string name = names[Array.IndexOf(new[] { "Idle", "Move", "Attack", "Death" }, role)];
            return Directory.GetFiles("Docs/model-art/" + spec.BodyFolder, "*.fbx")
                .Single(p => Path.GetFileNameWithoutExtension(p).Split('@').Last().Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        static void ImportBody(string path, float scale, Material material)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.globalScale = scale; importer.useFileScale = true;
            importer.importAnimation = false; importer.importCameras = false; importer.importLights = false;
            importer.optimizeGameObjects = false; importer.isReadable = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.SaveAndReimport();
            importer = (ModelImporter)AssetImporter.GetAtPath(path);
            foreach (var source in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>())
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(source), material);
            importer.SaveAndReimport();
        }

        /// <summary>모션 전용 FBX는 자체 골격으로 아바타를 만든다(주인공과 같은 방식). 루트 XZ 이동은 추출만 하고 적용하지 않아 제자리에서 재생된다.</summary>
        static void ImportClip(string path, string clipName, bool loop)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.globalScale = 1f; importer.useFileScale = true;
            importer.importAnimation = true; importer.importCameras = false; importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.SaveAndReimport();
            importer = (ModelImporter)AssetImporter.GetAtPath(path);
            var clips = importer.defaultClipAnimations;
            if (clips.Length == 0) throw new InvalidOperationException("Missing animation: " + path);
            var clip = clips[0]; clip.name = clipName; clip.loopTime = loop; clip.loopPose = loop;
            clip.lockRootRotation = true; clip.keepOriginalOrientation = true;
            clip.lockRootHeightY = true; clip.keepOriginalPositionY = true;
            clip.lockRootPositionXZ = false; clip.keepOriginalPositionXZ = true;
            importer.clipAnimations = new[] { clip };
            importer.SaveAndReimport();
        }

        public static AnimationClip Clip(string path) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));

        /// <summary>클립의 루트 이동 평균 속도를 대상 몸체의 humanScale로 환산한다(m/s).</summary>
        static float Stride(string clipPath, Animator body)
        {
            var clip = Clip(clipPath);
            var clipAvatar = AssetDatabase.LoadAllAssetsAtPath(clipPath).OfType<Avatar>().First();
            var probe = new GameObject("Stride Probe");
            try
            {
                var animator = probe.AddComponent<Animator>(); animator.avatar = clipAvatar;
                float ratio = animator.humanScale > 0f ? body.humanScale / animator.humanScale : 1f;
                return new Vector2(clip.averageSpeed.x, clip.averageSpeed.z).magnitude * ratio;
            }
            finally { Object.DestroyImmediate(probe); }
        }

        static AnimatorController Controller(Spec spec, string folder, Dictionary<string, string> clips, float moveTimeScale)
        {
            string path = folder + "/" + spec.Name + ".controller";
            AssetDatabase.DeleteAsset(path);
            var result = AnimatorController.CreateAnimatorControllerAtPath(path);
            result.AddParameter("Speed", AnimatorControllerParameterType.Float);
            result.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            result.AddParameter("Die", AnimatorControllerParameterType.Trigger);
            var machine = result.layers[0].stateMachine;
            var locomotion = machine.AddState("Locomotion");
            var tree = new BlendTree { name = "Idle Move", blendType = BlendTreeType.Simple1D, blendParameter = "Speed", useAutomaticThresholds = false };
            AssetDatabase.AddObjectToAsset(tree, result);
            tree.AddChild(Clip(clips["Idle"]), 0f); tree.AddChild(Clip(clips["Move"]), spec.MoveSpeed);
            var children = tree.children; children[1].timeScale = moveTimeScale; tree.children = children;
            locomotion.motion = tree; machine.defaultState = locomotion;
            var attack = machine.AddState("Attack"); attack.motion = Clip(clips["Attack"]); attack.speed = spec.AttackSpeed;
            var dead = machine.AddState("Dead"); dead.motion = Clip(clips["Death"]);
            var toAttack = locomotion.AddTransition(attack); toAttack.hasExitTime = false; toAttack.duration = .1f;
            toAttack.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
            var back = attack.AddTransition(locomotion); back.hasExitTime = true; back.exitTime = .9f; back.duration = .15f;
            if (spec.Name == "HammerBrute") { back.exitTime = 1f; back.duration = .05f; }
            var die = machine.AddAnyStateTransition(dead); die.hasExitTime = false; die.duration = .1f; die.canTransitionToSelf = false;
            die.AddCondition(AnimatorConditionMode.If, 0f, "Die");
            EditorUtility.SetDirty(result);
            return result;
        }

        static void Equip(Animator animator, string asset, HumanBodyBones boneId, float scale, Bounds body, List<string> report, string owner)
        {
            var bone = animator.GetBoneTransform(boneId) ?? (boneId == HumanBodyBones.Chest ? animator.GetBoneTransform(HumanBodyBones.Spine) : null);
            if (bone == null) throw new InvalidOperationException(owner + ": missing bone " + boneId);
            string socketName = boneId == HumanBodyBones.Chest ? "BackSocket" : boneId + "Socket";
            var socket = bone.Find(socketName) ?? new GameObject(socketName).transform;
            socket.SetParent(bone, false); socket.localPosition = Vector3.zero; socket.localRotation = Quaternion.identity;
            var wrapper = new GameObject(asset + "_Placement").transform; wrapper.SetParent(socket, false);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Equipment + "/" + asset + ".prefab"));
            if (model == null) throw new FileNotFoundException(asset + " equipment prefab is missing");
            model.transform.SetParent(wrapper, false); // FBX 축 변환은 모델 루트에 남긴다.
            Transform Find(string prefix) => model.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name.StartsWith(prefix, StringComparison.Ordinal));
            Bounds gearBounds = BoundsOf(model);

            // 장비 로컬 축: 긴 축은 소켓 쌍으로, 얇은 축은 바운즈 최소 축으로 정한다.
            wrapper.localRotation = Quaternion.identity; wrapper.localScale = Vector3.one;
            Vector3 anchorLocal, targetForward, targetUp; Vector3 longAxis, thinAxis;
            Transform anchor;
            if (asset == "ChiefCape")
            {
                anchor = Find("ShoulderAttach");
                longAxis = wrapper.InverseTransformDirection((gearBounds.center - anchor.position).normalized);
                thinAxis = ThinnestAxis(model, wrapper, longAxis);
                // 등 소켓: 정적 미리보기와 같은 좌표(1.8m 기준 (0,1.45,-.13)).
                socket.position = animator.transform.position + new Vector3(0, 1.45f, -.13f) * body.size.y / 1.8f;
                targetForward = Vector3.down; targetUp = Vector3.back;
            }
            else
            {
                anchor = Find("HandGrip");
                bool shield = asset.EndsWith("Shield");
                var tip = Find(shield ? "ShieldFace" : asset == "Warhammer" ? "ImpactCenter" : "BladeTip");
                longAxis = wrapper.InverseTransformDirection((tip.position - anchor.position).normalized);
                thinAxis = shield ? longAxis : ThinnestAxis(model, wrapper, longAxis);
                HandFrame(animator, boneId, out Vector3 fingers, out Vector3 thumb, out Vector3 palm, report, owner);
                if (shield)
                {
                    // Mixamo 검+방패 동작은 중앙 손잡이 방패 기준이다(막기 자세에서 손바닥이 정면을 향함): 앞면은 손바닥이 향하는 쪽, 세로축은 엄지 방향.
                    // 손 축 캡처(captures/*_idle_axes.png)로 확인한 규칙. 팔뚝 바깥 부착(앞면=-손바닥)은 막기 자세에서 방패가 눕는다.
                    longAxis = ThinnestAxis(model, wrapper, Vector3.zero, longest: true);
                    Vector3 face = wrapper.InverseTransformDirection((tip.position - anchor.position).normalized);
                    thinAxis = face; targetForward = palm; targetUp = thumb;
                    Vector3 tmp = longAxis; longAxis = thinAxis; thinAxis = tmp; // forward=면 법선, up=긴 축
                }
                else { targetForward = thumb; targetUp = palm; } // 칼날은 엄지 쪽, 얇은 면은 손바닥 법선
            }
            wrapper.localScale = Vector3.one * scale;
            Quaternion gearFrame = Quaternion.LookRotation(longAxis, Vector3.Cross(longAxis, thinAxis).sqrMagnitude < 1e-4f ? Vector3.up : thinAxis);
            Quaternion targetFrame = Quaternion.LookRotation(targetForward, targetUp);
            wrapper.rotation = targetFrame * Quaternion.Inverse(gearFrame);
            anchorLocal = anchor.position; // world
            model.transform.position += socket.position - anchorLocal;
            if (Vector3.Distance(anchor.position, socket.position) > .002f) throw new InvalidOperationException(asset + " grip offset " + Vector3.Distance(anchor.position, socket.position));
            if (asset == "ChiefCape") wrapper.localPosition = new Vector3(0f, -.065f, .03f);
            report.Add("  " + owner + " " + asset + " -> " + bone.name + " scale " + scale + " gripAt " + socket.position.ToString("F2"));
        }

        /// <summary>손 본의 실제 기하로 손가락·엄지·손바닥 방향을 구한다. 엄지가 없는 리그(우두머리)는 T포즈 관례(+Z)를 쓴다.</summary>
        static void HandFrame(Animator animator, HumanBodyBones hand, out Vector3 fingers, out Vector3 thumb, out Vector3 palm, List<string> report, string owner)
        {
            bool right = hand == HumanBodyBones.RightHand;
            var handBone = animator.GetBoneTransform(hand);
            var forearm = animator.GetBoneTransform(right ? HumanBodyBones.RightLowerArm : HumanBodyBones.LeftLowerArm);
            var knuckle = animator.GetBoneTransform(right ? HumanBodyBones.RightMiddleProximal : HumanBodyBones.LeftMiddleProximal)
                ?? animator.GetBoneTransform(right ? HumanBodyBones.RightIndexProximal : HumanBodyBones.LeftIndexProximal);
            var thumbBone = animator.GetBoneTransform(right ? HumanBodyBones.RightThumbProximal : HumanBodyBones.LeftThumbProximal);
            fingers = knuckle != null ? (knuckle.position - handBone.position).normalized : (handBone.position - forearm.position).normalized;
            string source = knuckle != null ? knuckle.name : "forearm";
            if (thumbBone != null)
            {
                thumb = Vector3.ProjectOnPlane(thumbBone.position - handBone.position, fingers).normalized;
                if (thumb.sqrMagnitude < .5f) thumbBone = null;
            }
            else thumb = Vector3.zero;
            if (thumbBone == null) { thumb = Vector3.ProjectOnPlane(Vector3.forward, fingers).normalized; source += "+assumedThumb"; }
            palm = Vector3.Cross(fingers, thumb).normalized * (right ? 1f : -1f);
            report.Add("  " + owner + " " + hand + " fingers " + fingers.ToString("F2") + " thumb " + thumb.ToString("F2") + " palm " + palm.ToString("F2") + " (" + source + ")");
        }

        static Vector3 ThinnestAxis(GameObject model, Transform space, Vector3 exclude, bool longest = false)
        {
            var mesh = model.GetComponentInChildren<MeshFilter>()?.sharedMesh ?? model.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh;
            var meshTransform = (model.GetComponentInChildren<MeshFilter>()?.transform) ?? model.GetComponentInChildren<SkinnedMeshRenderer>().transform;
            Vector3 best = Vector3.zero; float bestSize = longest ? -1f : float.MaxValue;
            foreach (var axis in new[] { Vector3.right, Vector3.up, Vector3.forward })
            {
                Vector3 world = meshTransform.TransformDirection(axis).normalized;
                Vector3 local = space.InverseTransformDirection(world);
                if (Mathf.Abs(Vector3.Dot(local, exclude)) > .7f) continue;
                float size = Vector3.Scale(mesh.bounds.size, axis).magnitude * meshTransform.lossyScale.magnitude;
                if (longest ? size > bestSize : size < bestSize) { bestSize = size; best = local; }
            }
            return best.normalized;
        }

        static void ConnectPrefabs(List<string> report)
        {
            foreach (var spec in Specs)
            {
                string visual = VisualPath(spec.Name);
                float death = Clip(Art + "/" + spec.Name + "/" + spec.Name + "_Death.fbx").length;
                if (spec.Name == "Swordsman") Apply(BasePrefab, spec, visual, death);
                string path = VariantPath(spec.Name);
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(BasePrefab));
                    try { instance.name = "Enemy_" + spec.Name; PrefabUtility.SaveAsPrefabAsset(instance, path); }
                    finally { Object.DestroyImmediate(instance); }
                }
                Apply(path, spec, visual, death);
                report.Add(spec.Name + " prefab: " + path + " (variant of Enemy.prefab)");
            }
        }

        static void Apply(string prefabPath, Spec spec, string visualPath, float deathLength)
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var visuals = root.GetComponent<EnemyVisuals>();
                visuals.visualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(visualPath);
                visuals.localPosition = Vector3.zero; visuals.localEulerAngles = Vector3.zero; visuals.localScale = Vector3.one;
                var attack = root.GetComponent<EnemyMeleeAttack>(); attack.windup = spec.Windup; attack.interval = spec.Interval;
                // 캐릭터별 지정 사거리를 적용하고, 미지정 시 기본 프리팹(Swordsman)을 따른다.
                attack.range = spec.AttackRange ?? AssetDatabase.LoadAssetAtPath<GameObject>(BasePrefab).GetComponent<EnemyMeleeAttack>().range;
                if (spec.Priorities is (int player, int minion, int tower, int core))
                {
                    var selector = root.GetComponent<EnemyTargetSelector>();
                    selector.playerPriority = player; selector.minionPriority = minion; selector.towerPriority = tower; selector.corePriority = core;
                }
                root.GetComponent<EnemyMotor>().moveSpeed = spec.MoveSpeed;
                root.GetComponent<UnityEngine.AI.NavMeshAgent>().speed = spec.MoveSpeed;
                root.GetComponent<EnemyHealth>().removeDelay = deathLength + .3f;
                EnemyScaleBuilder.Configure(root, spec.Name);
                if (spec.Name == EnemyShieldSetup.ShieldGuardName && prefabPath != BasePrefab) EnemyShieldSetup.Ensure(root);
                if (spec.Name == "Chief") ChiefSkillSetup.Ensure(root);
                visuals.RebuildVisual();
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        /// <summary>EnemyTest 씬 뒤쪽에 변형 4종을 한 줄로 세워 둔다. 기존 배치는 건드리지 않는다.</summary>
        static void PlaceShowcase()
        {
            string scenePath = Generated + "/EnemyTest.unity";
            if (!File.Exists(scenePath)) return;
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var group = GameObject.Find("Variant Showcase");
            if (group != null) Object.DestroyImmediate(group);
            group = new GameObject("Variant Showcase");
            int i = 0;
            foreach (var spec in Specs)
            {
                if (spec.Name == "Swordsman") continue;
                var enemy = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(VariantPath(spec.Name)), scene);
                enemy.transform.SetParent(group.transform, true);
                enemy.transform.position = new Vector3(-6f + 4f * i++, 0f, -9f);
            }
            EditorSceneManager.SaveScene(scene);
        }

        public static Bounds BoundsOf(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) throw new InvalidOperationException("No renderer: " + root.name);
            var bounds = renderers[0].bounds;
            foreach (var r in renderers.Skip(1)) bounds.Encapsulate(r.bounds);
            return bounds;
        }
    }
}
