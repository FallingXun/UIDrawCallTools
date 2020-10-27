using System;
using System.Collections;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
using UnityEditorInternal.Profiling;
#endif
using UnityEngine;

public class UIDrawCallTools : MonoBehaviour
{
    private int m_CurrentFrame = -1;
#if UNITY_EDITOR
    private ProfilerProperty m_Property = null;
#endif
    private bool m_CanSample = false;
    private int m_Drawcall = 0;
    private string m_Screen = "";
    private Timer m_Timer;
    // 保存子canvas的数据
    private Dictionary<int, int> m_Map = new Dictionary<int, int>();


    public static string m_Key = "UIDrawCallTools";

//#if UNITY_EDITOR
//    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
//    public static void OnStartGame()
//    {
//        Init();
//    }
//#endif

    public static void Init()
    {
#if UNITY_EDITOR
        var go = GameObject.Find(m_Key);
        if (go == null)
        {
            go = new GameObject(m_Key);
            GameObject.DontDestroyOnLoad(go);
            GameObject child = new GameObject();
            child.transform.SetParent(go.transform);
            child.AddComponent<UIDrawCallTools>();
        }
#endif
    }

    public static void UnInit()
    {
#if UNITY_EDITOR
        var go = GameObject.Find(m_Key);
        if (go != null)
        {
            GameObject.Destroy(go);
        }
#endif
    }

    public static void Refresh()
    {
#if UNITY_EDITOR
        var go = GameObject.Find(m_Key);
        if (go != null)
        {
            var dc = go.GetComponentInChildren<UIDrawCallTools>(true);
            if (dc != null)
            {
                dc.UpdateDrawCall();
                //dc.UpdateDrawCall(InArguments[0]);
            }
        }
#endif
    }

    private void Awake()
    {
        EventManager.ScreenOnOpen.AddEventHandler(ScreenOnOpen);
        EventManager.ScreenOnClose.AddEventHandler(ScreenOnClose);
    }

    private void OnDestroy()
    {
        EventManager.ScreenOnOpen.RemoveEventHandler(ScreenOnOpen);
        EventManager.ScreenOnClose.RemoveEventHandler(ScreenOnClose);
        EndSample();
        Reset();
        if (m_Timer != null)
        {
            m_Timer.StopTimer();
            m_Timer = null;
        }
    }

    public void UpdateDrawCall(string screen = "")
    {
#if UNITY_EDITOR
        EndSample();

        if (string.IsNullOrEmpty(screen) == false)
        {
            m_Screen = screen;
        }
        BeginSample();
#endif
    }

    private void ScreenOnOpen(ScreenBase screen)
    {
#if UNITY_EDITOR
        if (screen == null)
        {
            return;
        }
        m_Screen = screen.mStrUIName;
        if (m_Timer != null)
        {
            m_Timer.StopTimer();
            m_Timer = null;
        }
        m_Timer = Timer.Get(gameObject, 2, 1f).SetDelay(1f).SetUpdate(() =>
        {
            EndSample();
            BeginSample();
        });
        m_Timer.StartTimer();
#endif
    }


    private void ScreenOnClose(ScreenBase screen)
    {
#if UNITY_EDITOR
        if (screen == null)
        {
            return;
        }
        if (screen.mStrUIName.Equals(m_Screen) == false)
        {
            return;
        }
        EndSample();
        Reset();
        if (m_Timer != null)
        {
            m_Timer.StopTimer();
            m_Timer = null;
        }
#endif
    }

    private void Reset()
    {
#if UNITY_EDITOR

        m_Drawcall = 0;
        m_Screen = "";

        if (m_Property != null)
        {
            m_Property.Dispose();
            m_Property = null;
        }
#endif
    }

    private void Update()
    {
#if UNITY_EDITOR
        Sample(m_Screen);
        if (m_CurrentFrame > ProfilerDriver.lastFrameIndex)
        {
            m_CurrentFrame = ProfilerDriver.lastFrameIndex;
        }
#endif
    }

#if UNITY_EDITOR
    void OnGUI()
    {
        string colorStr = "#00FF00";
        if (m_Drawcall >= 30)
        {
            colorStr = "#FF0000";
        }

        Rect rect = new Rect(Screen.width * 0.02f, Screen.height * 0.93f, 250, 40);

        string label = "";
        if (m_Drawcall <= 0)
        {
            label = string.Format("<size=15><b><color=#00FF00>{0}</color></b></size>", "drawcall tool is on");

        }
        else
        {
            label = string.Format("<size=15><b><color=#00FF00>{0}:</color><color={1}>{2}</color></b></size>", m_Screen, colorStr, m_Drawcall);
        }

        GUI.Label(rect, label);
    }
#endif

    private void BeginSample()
    {
#if UNITY_EDITOR
        m_CanSample = true;
        ProfilerDriver.profileEditor = true;
        ProfilerDriver.enabled = true;
        m_Map.Clear();
#endif
    }

    private void EndSample()
    {
#if UNITY_EDITOR
        m_CanSample = false;
        ProfilerDriver.profileEditor = false;
        ProfilerDriver.enabled = false;
        ProfilerDriver.ClearAllFrames();
        m_Map.Clear();
#endif
    }

    private void Sample(string screen)
    {
#if UNITY_EDITOR
        if (m_CanSample == false)
        {
            return;
        }
        if (string.IsNullOrEmpty(screen))
        {
            EndSample();
            return;
        }
        if (m_Property != null)
        {
            m_Property.Dispose();
            m_Property = null;
        }

        if (ProfilerDriver.firstFrameIndex == -1 && ProfilerDriver.lastFrameIndex == -1)
        {
            return;
        }

        int targetedFrame = Mathf.Max(ProfilerDriver.firstFrameIndex, m_CurrentFrame + 1);

        int lastFrame = ProfilerDriver.lastFrameIndex;

        if (targetedFrame > lastFrame)
        {
            return;
        }
        m_Property = new ProfilerProperty();
        m_Property.SetRoot(targetedFrame, ProfilerColumn.DontSort, ProfilerViewType.Hierarchy);

        if (m_Property == null || m_Property.frameDataReady == false)
        {
            return;
        }
        UISystemProfilerInfo[] UISystemData = m_Property.GetUISystemProfilerInfo();
        //int[] allBatchesInstanceIDs = m_Property.GetUISystemBatchInstanceIDs();

        if (UISystemData != null)
        {
            m_Map.Clear();

            int batchIndex = 0;
            foreach (var data in UISystemData)
            {
                if (data.isBatch)
                {
                    batchIndex++;
                    // 是要检测的目标面板和其嵌套canvas，则计算
                    if (m_Map.ContainsKey(data.parentId))
                    {
                        m_Map[data.parentId] = batchIndex;
                    }
                    //Debug.Log(data.objectInstanceId + "     " + data.parentId + "     " + data.totalBatchCount);
                }
                else
                {
                    name = m_Property.GetUISystemProfilerNameByOffset(data.objectNameOffset);
                    batchIndex = 0;

                    // 如果是要检测的目标面板，加入字典
                    if (name.Equals(screen))
                    {
                        m_Map[data.objectInstanceId] = 0;
                    }
                    else
                    {
                        // 如果是目标面板的嵌套canvas，加入字典
                        if (m_Map.ContainsKey(data.parentId))
                        {
                            m_Map[data.objectInstanceId] = 0;
                        }
                        // 找到当前的canvas，设置了override sortting的，parentId为0，需要依次往上遍历检查是否为目标面板
                        var canvas = EditorUtility.InstanceIDToObject(data.objectInstanceId) as Canvas;
                        if (canvas != null)
                        {
                            Canvas[] list = canvas.GetComponentsInParent<Canvas>(true);
                            if (list != null)
                            {
                                for (int i = 0; i < list.Length; i++)
                                {
                                    if (m_Map.ContainsKey(list[i].GetInstanceID()))
                                    {
                                        m_Map[data.objectInstanceId] = 0;
                                        break;
                                    }
                                }
                            }
                        }
                    }

                    //Debug.Log(name + "  " + data.objectInstanceId + "     " + data.parentId);
                }
            }
            int batch = 0;
            foreach (var item in m_Map)
            {
                batch += item.Value;
            }
            m_Screen = screen;
            m_Drawcall = batch;
            //DebugHelper.Log(screen + ":" + batch);
        }
        else
        {
            DebugHelper.Log("UISystemData is null!");
        }
        EndSample();
#endif
    }
}
