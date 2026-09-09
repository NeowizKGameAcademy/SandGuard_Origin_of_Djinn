using UnityEngine;

namespace SandGuard.Enemy
{
    /// <summary>Only used by EnemyArtPreview. Does not drive combat or character animation.</summary>
    public sealed class EnemyArtPreviewControls : MonoBehaviour
    {
        public Camera previewCamera;
        public Transform[] displays;
        public Transform[] stationLabels;
        public GameObject[] equipmentGroups;
        public string[] displayNames;
        public float[] heights;
        public Animator[] capeAnimators;
        public Vector3[] lineupPositions;
        public int selection = -1;
        public int view;
        public bool showEquipment = true;
        public bool animateCape = true;
        public bool showInterface = true;
        public float gameDistance = 2f;
        public float gamePitch = 15f;
        public float gameFieldOfView = 60f;
        public float gamePivotHeight = 1.6f;
        public float gamePitchLift = .5f;
        public float gameMaxPitch = 70f;
        public float gameShoulderOffset = .5f;
        private float orbit;
        private float zoom = 1f;

        void Start() => ApplyView();

        public void SelectCharacter(int index)
        {
            selection = Mathf.Clamp(index, -1, displays.Length - 1);
            if (selection < 0 && view == 3) view = 0;
            zoom = 1f;
            ApplyView();
        }

        public void SetView(int index)
        {
            view = Mathf.Clamp(index, 0, 3);
            if (view == 3 && selection < 0) selection = 0;
            orbit = 0; zoom = 1f;
            ApplyView();
        }

        public void SetEquipmentVisible(bool visible)
        {
            showEquipment = visible;
            foreach (var group in equipmentGroups) if (group != null) group.SetActive(visible);
        }

        public void ApplyView()
        {
            if (previewCamera == null || displays == null) return;
            float yaw = (view == 1 ? 90f : view == 2 ? 180f : 0f) + orbit;
            for (int i = 0; i < displays.Length; ++i)
            {
                if (displays[i] == null) continue;
                displays[i].gameObject.SetActive(selection < 0 || i == selection);
                displays[i].localPosition = selection < 0 ? lineupPositions[i] : Vector3.zero;
                // Rotate each station independently so side/rear lineups never overlap.
                displays[i].localRotation = Quaternion.Euler(0, yaw, 0);
                if (stationLabels != null && i < stationLabels.Length && stationLabels[i] != null)
                {
                    stationLabels[i].position = displays[i].position + new Vector3(0,.03f,.84f);
                    stationLabels[i].rotation = Quaternion.Euler(60,180,0);
                }
            }
            SetEquipmentVisible(showEquipment);
            foreach (var animator in capeAnimators) if (animator != null) animator.speed = animateCape ? 1f : 0f;
            if (view == 3)
            {
                previewCamera.orthographic = false;
                previewCamera.fieldOfView = gameFieldOfView;
                var rotation = Quaternion.Euler(gamePitch, 180, 0);
                var pivot = new Vector3(0, gamePivotHeight + Mathf.Clamp01(gamePitch / Mathf.Max(1,gameMaxPitch)) * gamePitchLift, 0);
                previewCamera.transform.SetPositionAndRotation(pivot + rotation * new Vector3(gameShoulderOffset, 0, -gameDistance * zoom), rotation);
            }
            else
            {
                previewCamera.orthographic = true;
                // Fit the lineup on narrow Game views as well as a 16:9 monitor.
                float fit = selection < 0 ? Mathf.Max(1.85f, 5.0f / Mathf.Max(.4f, previewCamera.aspect)) : 1.62f;
                previewCamera.orthographicSize = fit * zoom;
                previewCamera.transform.position = new Vector3(0, 2.25f, 11);
                previewCamera.transform.LookAt(new Vector3(0, 1.03f, 0));
            }
        }

        void OnGUI()
        {
            if (!showInterface) return;
            float scale = Mathf.Clamp(Screen.width / 1280f, .65f, 1.4f);
            var oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
            float width = Screen.width / scale;
            float panelWidth = Mathf.Min(1160, width - 32);
            float characterButtonWidth = Mathf.Clamp((panelWidth - 145) / 5, 65, 170);
            GUILayout.BeginArea(new Rect(16, 12, panelWidth, 152), GUI.skin.box);
            GUILayout.Label("적 외형 확인 — 몸체 A포즈 / 장비 임시 장착");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(selection < 0 ? "● 전체 5종" : "전체 5종", GUILayout.Width(100))) SelectCharacter(-1);
            for (int i = 0; i < displayNames.Length; ++i)
                if (GUILayout.Button((selection == i ? "● " : "") + displayNames[i] + (characterButtonWidth >= 130 ? "  " + heights[i].ToString("F2") + "m" : ""), GUILayout.Width(characterButtonWidth))) SelectCharacter(i);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            string[] modes = { "정면", "측면", "후면", "게임 거리 " + gameDistance.ToString("F1") + "m" };
            for (int i = 0; i < modes.Length; ++i)
                if (GUILayout.Button((view == i ? "● " : "") + modes[i], GUILayout.Width(Mathf.Min(160,(panelWidth - 32)/4)))) SetView(i);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            bool equipment = GUILayout.Toggle(showEquipment, "장비 표시", GUILayout.Width(100));
            bool cape = GUILayout.Toggle(animateCape, "망토 흔들림", GUILayout.Width(120));
            if (equipment != showEquipment) SetEquipmentVisible(equipment);
            if (cape != animateCape) { animateCape = cape; ApplyView(); }
            if (GUILayout.Button("시점 초기화", GUILayout.Width(110))) SetView(view);
            GUILayout.EndHorizontal();
            GUILayout.Label("빈 화면 드래그: 회전  ·  휠: 확대/축소  ·  게임 거리 기본: " + gameDistance.ToString("F1") + "m / " + gamePitch.ToString("F0") + "° / FOV " + gameFieldOfView.ToString("F0") + "°" + (view == 3 ? "   현재 " + (gameDistance * zoom).ToString("F1") + "m" : ""));
            GUILayout.EndArea();
            GUI.Box(new Rect(16, Screen.height / scale - 47, Mathf.Min(940, width - 32), 31), "장착 위치 확인용입니다. 손가락 쥐기·공격·방어 자세는 Mixamo 애니메이션 연결 후 조정합니다.");
            GUI.matrix = oldMatrix;
            var e = Event.current;
            if (e.mousePosition.y < 172 * scale || e.mousePosition.y > Screen.height - 60 * scale) return;
            if (e.type == EventType.MouseDrag && e.button == 0) { orbit -= e.delta.x * .5f; ApplyView(); e.Use(); }
            if (e.type == EventType.ScrollWheel) { zoom = Mathf.Clamp(zoom + e.delta.y * .035f, .6f, view == 3 ? 4f : 1.8f); ApplyView(); e.Use(); }
        }
    }
}
