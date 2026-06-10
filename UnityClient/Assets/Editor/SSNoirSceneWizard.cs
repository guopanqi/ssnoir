using UnityEngine;
using UnityEditor;
using Cinemachine;
using System.Linq;

namespace SSNoir.Editor
{
    public class SSNoirSceneWizard : EditorWindow
    {
        [MenuItem("SSNoir/Setup Rendering Scene")]
        public static void SetupScene()
        {
            Undo.IncrementCurrentGroup();
            string groupName = "SSNoir Scene Setup";

            SetupMainCamera(groupName);
            var globalCam = CreateGlobalCamera(groupName);
            var focusCam = CreateFocusCamera(groupName);

            CreateHomeAnchors(groupName);
            CreateOfficeAnchors(groupName);

            CreateSceneManager(globalCam, focusCam, groupName);

            Debug.Log("[SSNoir] Scene setup complete! Open the SceneDirectory and SSNoirGameManager to verify references.");
        }

        private static void SetupMainCamera(string undoGroup)
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var existing = GameObject.Find("Main Camera");
                if (existing != null) cam = existing.GetComponent<Camera>();
            }

            if (cam == null)
            {
                var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                cam = camGo.GetComponent<Camera>();
                cam.tag = "MainCamera";
                cam.transform.position = new Vector3(0, 6, -8);
                cam.transform.rotation = Quaternion.Euler(20, 0, 0);
                Undo.RegisterCreatedObjectUndo(camGo, undoGroup);
            }

            if (cam.GetComponent<CinemachineBrain>() == null)
            {
                var brain = cam.gameObject.AddComponent<CinemachineBrain>();
                Undo.RegisterCreatedObjectUndo(brain, undoGroup);
            }
        }

        private static CinemachineVirtualCamera CreateGlobalCamera(string undoGroup)
        {
            var existing = GameObject.Find("VCam_Global");
            if (existing != null) return existing.GetComponent<CinemachineVirtualCamera>();

            var go = new GameObject("VCam_Global", typeof(CinemachineVirtualCamera));
            go.transform.position = new Vector3(2, 8, -6);
            go.transform.rotation = Quaternion.Euler(40, -15, 0);

            var vcam = go.GetComponent<CinemachineVirtualCamera>();
            vcam.Priority = 10;

            Undo.RegisterCreatedObjectUndo(go, undoGroup);
            return vcam;
        }

        private static CinemachineVirtualCamera CreateFocusCamera(string undoGroup)
        {
            var existing = GameObject.Find("VCam_Focus");
            if (existing != null) return existing.GetComponent<CinemachineVirtualCamera>();

            var go = new GameObject("VCam_Focus", typeof(CinemachineVirtualCamera));
            go.transform.position = new Vector3(0, 2, -3);
            go.transform.rotation = Quaternion.identity;

            var vcam = go.GetComponent<CinemachineVirtualCamera>();
            vcam.Priority = 5;

            Undo.RegisterCreatedObjectUndo(go, undoGroup);
            return vcam;
        }

        private static void CreateHomeAnchors(string undoGroup)
        {
            CreateNodeAnchor("踢垃圾桶", new Vector3(-3.5f, 0, 0), null, undoGroup);
            CreateNodeAnchor("清理垃圾", new Vector3(-2.5f, 0, 1.5f), null, undoGroup);
            CreateNodeAnchor("敲门", new Vector3(0, 0, 3), null, undoGroup);

            var enterFocus = CreateFocusTransform("进门2", new Vector3(0.5f, 1.5f, 5));
            CreateNodeAnchor("进门2", new Vector3(0, 0, 4), enterFocus, undoGroup);

            CreateNodeAnchor("买一盆花", new Vector3(2, 0, 3), null, undoGroup);
            CreateNodeAnchor("一盆花", new Vector3(3, 0, 3.5f), null, undoGroup);
            CreateNodeAnchor("买一瓶酒", new Vector3(4, 0, 2), null, undoGroup);
            CreateNodeAnchor("喝酒", new Vector3(5, 0, 1), null, undoGroup);
            CreateNodeAnchor("家", new Vector3(0, 0, 0), null, undoGroup);
        }

        private static void CreateOfficeAnchors(string undoGroup)
        {
            CreateNodeAnchor("写代码", new Vector3(10, 0, 0), null, undoGroup);

            var homeFocus = CreateFocusTransform("回家", new Vector3(12, 1.5f, -2));
            CreateNodeAnchor("回家", new Vector3(12, 0, -1), homeFocus, undoGroup);

            CreateNodeAnchor("办公室", new Vector3(10, 0, 2), null, undoGroup);
        }

        private static Transform CreateFocusTransform(string anchorName, Vector3 position)
        {
            var go = new GameObject($"Focus_{anchorName}");
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(15, 180, 0);
            return go.transform;
        }

        private static void CreateNodeAnchor(string nodeName, Vector3 position, Transform focusTransform, string undoGroup)
        {
            var go = new GameObject($"Anchor_{nodeName}", typeof(NodeAnchor));
            go.transform.position = position;

            var anchor = go.GetComponent<NodeAnchor>();
            anchor.NodeName = nodeName;
            anchor.FocusCameraTransform = focusTransform;

            Undo.RegisterCreatedObjectUndo(go, undoGroup);
        }

        private static void CreateSceneManager(CinemachineVirtualCamera globalCam, CinemachineVirtualCamera focusCam, string undoGroup)
        {
            // SceneDirectory
            var sdGo = new GameObject("SceneDirectory", typeof(SceneDirectory));
            Undo.RegisterCreatedObjectUndo(sdGo, undoGroup);

            // SSNoirGameManager
            var gmGo = new GameObject("SSNoirGameManager", typeof(SSNoirGameManager));
            var gm = gmGo.GetComponent<SSNoirGameManager>();

            var serialized = new SerializedObject(gm);
            serialized.FindProperty("globalCamera").objectReferenceValue = globalCam;
            serialized.FindProperty("focusCamera").objectReferenceValue = focusCam;
            serialized.ApplyModifiedProperties();

            Undo.RegisterCreatedObjectUndo(gmGo, undoGroup);
        }
    }
}
