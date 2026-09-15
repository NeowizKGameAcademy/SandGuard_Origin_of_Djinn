#if UNITY_EDITOR
using System.Linq;
using SandGuard.Skills.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SandGuard.UI.HUD.Editor
{
    public static class SkillHUDWiring
    {
        [MenuItem("SandGuard/Skills/Connect Skill Tree To Game HUD")]
        public static void Connect()
        {
            var selected=Selection.activeGameObject;
            var session=selected?selected.GetComponentInChildren<SkillTreeSession>(true):null;
            if(!session)
            {
                var sessions=Object.FindObjectsByType<SkillTreeSession>(FindObjectsSortMode.None).Where(s=>s.isActiveAndEnabled).ToArray();
                if(sessions.Length!=1){Debug.LogError("연결할 SkillTreeSession 오브젝트를 선택하세요. 세션이 여러 개면 자동 선택하지 않습니다.");return;}
                session=sessions[0];
            }
            var windows=Object.FindObjectsByType<SkillTreeWindow>(FindObjectsSortMode.None);
            var window=windows.FirstOrDefault(w=>w.session==session);
            var huds=Object.FindObjectsByType<GameHUDController>(FindObjectsSortMode.None).Where(h=>h.gameObject.scene==session.gameObject.scene).ToArray();
            if(huds.Length==0){Debug.LogError("같은 씬에 GameHUDCanvas를 먼저 배치하세요.");return;}
            foreach(var hud in huds)
            {
                var link=hud.GetComponent<SkillTreeHUDLink>();if(!link)link=Undo.AddComponent<SkillTreeHUDLink>(hud.gameObject);
                Undo.RecordObject(link,"Connect skill tree HUD");link.hud=hud;link.session=session;
                if(window){link.theme=window.theme;if(window.player)link.player=window.player.gameObject;}
                var presenter=hud.GetComponent<GameHUDPresenter>();if(!link.player && presenter)link.player=presenter.player;
                if(link.player)
                {
                    var executor=link.player.GetComponent<PlayerSkillTreeExecutor>();
                    if(!executor)executor=Undo.AddComponent<PlayerSkillTreeExecutor>(link.player);
                    Undo.RecordObject(executor,"Connect player skill executor");executor.session=session;link.stateSource=executor;
                    EditorUtility.SetDirty(executor);PrefabUtility.RecordPrefabInstancePropertyModifications(executor);
                }
                EditorUtility.SetDirty(link);PrefabUtility.RecordPrefabInstancePropertyModifications(link);EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);
            }
            Debug.Log("스킬트리 → GameHUD 연결 완료. 씬을 저장하세요. 플레이어 실행기도 함께 연결했습니다.");
        }
        [CustomEditor(typeof(SkillTreeHUDLink))]
        public sealed class Inspector : UnityEditor.Editor
        {
            public override void OnInspectorGUI()
            {
                DrawDefaultInspector();var link=(SkillTreeHUDLink)target;
                EditorGUILayout.HelpBox("구매·장착 상태와 HUD를 연결합니다. PlayerSkillTreeExecutor가 실제 사용을 처리합니다. 데모 실행기이면 장착 아이콘에 미연결 상태를 표시합니다.",MessageType.Info);
                if(Application.isPlaying)for(int i=0;i<5;i++)EditorGUILayout.LabelField(((SandGuard.Skills.EquipSlot)i).ToString(),link.SlotStatus[i]??"세션 대기");
            }
        }
    }
}
#endif
