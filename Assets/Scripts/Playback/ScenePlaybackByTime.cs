// Copyright 2020 The Tilt Brush Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush
{

    public class StrokePlaybackByTime : StrokePlayback
    {
        private LinkedListNode<Stroke> m_strokeNode;

        public LinkedListNode<Stroke> StrokeNode
        {
            get { return m_strokeNode; }
        }

        public void Init(LinkedListNode<Stroke> memoryObjectNode,
                         PointerScript pointer, CanvasScript canvas, StrokeIndicator indicator, OculusAvatar avatar, VrControllers controller)
        {
            m_strokeNode = memoryObjectNode;
            Debug.Log("Before base init");
            BaseInit(memoryObjectNode.Value, pointer, canvas, indicator, avatar, controller);
        }

        public override void ClearPlayback()
        {
            m_strokeNode = null;
            base.ClearPlayback();
        }

        protected override bool IsControlPointReady(PointerManager.ControlPoint controlPoint, double TotalCurrentPauedTimeMs)
        {
            // TODO: API accepts time source function
            return (controlPoint.m_TimestampMs / 1000F) <= (App.Instance.CurrentSketchTime - TotalCurrentPauedTimeMs / 1000F);
        }
    }

    // Playback using stroke timestamps and supporting layering in time and timeline scrub.
    //
    // When moving forward we need strokes ordered by head timestamp so that we can schedule
    // rendering, and when moving backward we need tail timestamp ordering so that we can
    // delete the minimum set of affected strokes.  Our stroke accounting has them exist in
    // one of three places:
    //     1) an "unrendered" linked list (ordered by head timestamp)
    //     2) assigned to a pointer for rendering
    //     3) a "rendered" linked list (ordered by tail timestamp)
    //
    // For our use patterns, the insertions into the ordered linked lists are
    // effectively O(1) complexity:
    //     * strokes are added to rendered list in order after each is completed, so
    //       insert will traverse at most num_pointers nodes
    //     * strokes are added to unrendered list in order from the head of rendered list, so
    //       insert will traverse at most num_overlapping_strokes nodes (i.e. number of
    //       strokes overlapping in time with the inserted stroke)
    public class ScenePlaybackByTimeLayered : IScenePlayback
    {
        // Array of pending stroke playbacks indexed by pointer.
        private StrokePlaybackByTime[] m_strokePlaybacks;
        private int m_lastTimeMs = 0;
        // List of unrendered strokes ordered by head timestamp, earliest first
        private SortedLinkedList<Stroke> m_unrenderedStrokes;
        // List of rendered strokes ordered by tail timestamp, latest first
        private SortedLinkedList<Stroke> m_renderedStrokes;
        private int m_strokeCount;

        //private int m_popedStrokeCount = 0;
        //private int m_prepopedStrokeCount = 0;
        private uint m_lastStrokeTailTimeMs = 0;
        private uint m_nextStrokeHeadTimeMs = 0;
        private Vector3 m_lastHeadPosition;
        private Quaternion m_lastHeadOrient;
        private Vector3 m_nextHeadPosition;
        private Quaternion m_nextHeadOrient;
        private Vector3 m_lastControllerPosition;
        private Quaternion m_lastControllerOrient;
        private Vector3 m_nextControllerPosition;
        private Quaternion m_nextControllerOrient;
        private Vector3 m_lastIndicatorPosition;
        private Quaternion m_lastIndicatorOrient;
        private Vector3 m_nextIndicatorPosition;
        private Quaternion m_nextIndicatorOrient;

        private int m_maxPointerUnderrun = 0;
        private CanvasScript m_targetCanvas;

        public int MaxPointerUnderrun { get { return m_maxPointerUnderrun; } }
        public int MemoryObjectsDrawn { get { return 0; } } // unimplemented
        public double TotalPausedTimeMs = 0.0;
        public double TotalAdjustedTimeMs = 0.0;
        public double LastPausedTimeMs = 0.0;
        public double CurrentPausedTimeMs = 0.0;
        public bool IsPaused = false;

        // Input strokes must be ordered by head timestamp
        public ScenePlaybackByTimeLayered(IEnumerable<Stroke> strokes)
        {
            m_targetCanvas = App.ActiveCanvas;
            m_unrenderedStrokes = new SortedLinkedList<Stroke>(
                (a, b) => (a.HeadTimestampMs < b.HeadTimestampMs),
                strokes);
            m_strokeCount = m_unrenderedStrokes.Count;
            m_renderedStrokes = new SortedLinkedList<Stroke>(
                (a, b) => (a.TailTimestampMs >= b.TailTimestampMs),
                new Stroke[] { });
            m_strokePlaybacks = new StrokePlaybackByTime[PointerManager.m_Instance.NumTransientPointers];
            for (int i = 0; i < m_strokePlaybacks.Length; ++i)
            {
                m_strokePlaybacks[i] = new StrokePlaybackByTime();
            }
        }

        // Continue drawing stroke for this frame, returning true if more rendering is pending.
        public bool Update()
        {
            Debug.Log("ScenePlayBackByTime in Update.");
            var isSyncPosition = false;
            if (InputManager.m_Instance.GetCommandHeld(InputManager.SketchCommands.SyncPosition))
            {
                OutputWindowScript.m_Instance.CreateInfoCardAtController(
                            InputManager.ControllerName.Brush, "Sync cameraposition with instructor position");
                PlayBackObject.m_Instance.SyncHeadPosition();
                isSyncPosition = true;
            }
            if (InputManager.m_Instance.GetCommandDown(InputManager.SketchCommands.SyncBrush) && !isSyncPosition)
            {
                var durableName = PointerManager.m_Instance.LoadTransientBrushInfo();
                OutputWindowScript.m_Instance.CreateInfoCardAtController(
                            InputManager.ControllerName.Brush, "Sync up the current brush: " + durableName);
            }
            if (InputManager.m_Instance.GetCommandDown(InputManager.SketchCommands.Pause))
            {
                Debug.Log("Receive the Pause signal.");
                if (IsPaused)
                {
                    IsPaused = false;
                    TotalPausedTimeMs += App.Instance.CurrentSketchTime * 1000 - LastPausedTimeMs;

                } else
                {
                    IsPaused = true;
                    LastPausedTimeMs = App.Instance.CurrentSketchTime * 1000;
                }
            }
            Debug.LogFormat("Pause is {0}", IsPaused);
            CurrentPausedTimeMs = 0;
            if (IsPaused)
            {
                CurrentPausedTimeMs = App.Instance.CurrentSketchTime * 1000 - LastPausedTimeMs;
            }

            double TotalCurrentPauedTimeMs = TotalPausedTimeMs + CurrentPausedTimeMs - TotalAdjustedTimeMs;
            double currentTimeMsWithoutEdit;
            double adjustedTimeMs = 0.0;
            currentTimeMsWithoutEdit = (App.Instance.CurrentSketchTime * 1000) - TotalCurrentPauedTimeMs;

            if (InputManager.m_Instance.GetCommandDown(InputManager.SketchCommands.Forward) && !IsPaused)
            {
                if (m_nextStrokeHeadTimeMs != 0)
                {
                    adjustedTimeMs = m_nextStrokeHeadTimeMs - currentTimeMsWithoutEdit;
                    TotalAdjustedTimeMs += adjustedTimeMs;
                    OutputWindowScript.m_Instance.CreateInfoCardAtController(
                            InputManager.ControllerName.Brush, "Forward to next stroke");
                }
                
            }
            if (InputManager.m_Instance.GetCommandDown(InputManager.SketchCommands.Backward) && !IsPaused)
            {

                if (m_renderedStrokes.Count > 0 )
                {
                    double offset = 10;
                    var previousStroke = GetNextVisibleStroke(m_renderedStrokes);
                    adjustedTimeMs = previousStroke.HeadTimestampMs - currentTimeMsWithoutEdit - offset;
                    TotalAdjustedTimeMs += adjustedTimeMs;
                    OutputWindowScript.m_Instance.CreateInfoCardAtController(
                        InputManager.ControllerName.Brush, "Backward to previous stroke");
                }
            }

            int currentTimeMs;
            currentTimeMs = (int)(currentTimeMsWithoutEdit + adjustedTimeMs);
            if (currentTimeMs < 0)
            {
                currentTimeMs = 0;
            }
            
            Debug.LogFormat("TotalPausedTimeMs is {0}", TotalPausedTimeMs);
            Debug.LogFormat("CurrentPausedTimeMs is {0}", CurrentPausedTimeMs);
            Debug.LogFormat("currentTimeMs is {0}", currentTimeMs);

            // Handle a jump back in time by resetting corresponding in-flight or completed strokes
            // to the undrawn state.
            if (currentTimeMs < m_lastTimeMs)
            {
                Debug.Log("currentTimeMs is less than m_lastTimeMs");
                // any stroke in progress is implicated by rewind-- clear the stroke's playback
                foreach (var stroke in m_strokePlaybacks)
                {
                    if (!stroke.IsDone())
                    {
                        var pendingNode = stroke.StrokeNode;
                        stroke.ClearPlayback();
                        SketchMemoryScript.m_Instance.UnrenderStrokeMemoryObject(pendingNode.Value);
                        m_unrenderedStrokes.Insert(pendingNode);
                    }
                }
                // delete any stroke having final timestamp > new current time
                while (m_renderedStrokes.Count > 0 &&
                    m_renderedStrokes.First.Value.TailTimestampMs > currentTimeMs)
                {
                    var node = m_renderedStrokes.PopFirst();
                    if (node.Value.IsVisibleForPlayback)
                    {
                        // TODO: remove SketchMemory cyclical dependency
                        // TODO: sub-stroke unrender to eliminate needless geometry thrashing within a frame
                        SketchMemoryScript.m_Instance.UnrenderStrokeMemoryObject(node.Value);
                    }
                    m_unrenderedStrokes.Insert(node);
                }
            }

            int pendingStrokes = 0;
            int pendingVisibleStrokes = 0;
            if (currentTimeMs != 0)
            {
                Debug.Log("PlayBack by Time Update() before for loop.");
                Debug.LogFormat("m_strokePlaybacks.Length is {0}", m_strokePlaybacks.Length);
                for (int i = 0; i < m_strokePlaybacks.Length; ++i)
                {
                    var stroke = m_strokePlaybacks[i];
                    // update any pending stroke from last frame
                    Debug.Log("update any pending stroke from last frame");             
                    stroke.Update(TotalCurrentPauedTimeMs);
                    Debug.Log("Checking stroke is done: " + stroke.IsDone());
                    if (stroke.IsDone() && stroke.StrokeNode != null)
                    {
                        m_renderedStrokes.Insert(stroke.StrokeNode);
                        stroke.ClearPlayback();
                    }
                    // grab and play available strokes, until one is left pending
                    while (stroke.IsDone() && m_unrenderedStrokes.Count > 0 &&
                        (m_unrenderedStrokes.First.Value.HeadTimestampMs <= currentTimeMs ||
                        !m_unrenderedStrokes.First.Value.IsVisibleForPlayback))
                    {
                        var node = m_unrenderedStrokes.PopFirst();
                        // what if the next stroke is not visible
                        /*if (m_unrenderedStrokes.Count > 0 && m_unrenderedStrokes.First.Value.IsDrawForPlayback && node.Value.IsDrawForPlayback)*/
                        if (m_unrenderedStrokes.Count > 0 && node.Value.IsDrawForPlayback)
                        {
                            Debug.Log("Start to record next Stroke info");
                            var nextStroke = GetNextVisibleStroke(m_unrenderedStrokes);
                            if (nextStroke != null)
                            {
                                m_nextStrokeHeadTimeMs = nextStroke.HeadTimestampMs;

                                m_nextHeadPosition = nextStroke.FirstHeadPosition;
                                m_nextHeadOrient = nextStroke.FirstHeadOrient;

                                m_nextControllerPosition = nextStroke.FirstControllerPosition;
                                m_nextControllerOrient = nextStroke.FirstControllerOrient;

                                m_nextIndicatorPosition = nextStroke.FirstIndicatorPosition;
                                m_nextIndicatorOrient = nextStroke.FirstIndicatorOrient;

                                Debug.Log("Finish to record next Stroke info");
                                m_lastStrokeTailTimeMs = node.Value.TailTimestampMs;

                                m_lastHeadPosition = node.Value.LastHeadPosition;
                                m_lastHeadOrient = node.Value.LastHeadOrient;

                                m_lastControllerPosition = node.Value.LastControllerPosition;
                                m_lastControllerOrient = node.Value.LastControllerOrient;

                                m_lastIndicatorPosition = node.Value.LastIndicatorPosition;
                                m_lastIndicatorOrient = node.Value.LastIndicatorOrient;
                                Debug.Log("Finish to record last Stroke info");
                            }
                        }
                        // m_popedStrokeCount++;
                        Debug.Log("Before checking the visibility of stroke: " + node.Value.IsVisibleForPlayback);
                        if (node.Value.IsVisibleForPlayback)
                        {
                            Debug.Log("Before init the stroke");
                            Debug.Log("Check the i value: " + i);
                            Debug.Log("get the transientpointer {0}", PointerManager.m_Instance.GetTransientPointer(i));
                            Debug.Log("Check the total length: " + m_strokePlaybacks.Length);
                            stroke.Init(node, PointerManager.m_Instance.GetTransientPointer(i), m_targetCanvas, PlayBackObject.m_Instance.GetIndicator(), PlayBackObject.m_Instance.GetAvatar(), PlayBackObject.m_Instance.GetController());
                            // Load the current playback storke into the transient brush
                            if (node.Value.IsDrawForPlayback)
                            {
                                PointerManager.m_Instance.StoreTransientBrushInfo(node.Value);
                            }
                            Debug.Log("update the strokes after it has been init");
                            stroke.Update(TotalCurrentPauedTimeMs);
                            if (stroke.IsDone())
                            {
                                m_renderedStrokes.Insert(stroke.StrokeNode);
                                //m_lastStrokeTailTimeMs = stroke.StrokeNode.Value.TailTimestampMs;
                                //m_lastHeadPosition = stroke.StrokeNode.Value.LastHeadPosition;
                                //m_lastHeadOrient = stroke.StrokeNode.Value.LastHeadOrient;
                                stroke.ClearPlayback();
                            }
                        }
                        else
                        {
                            m_renderedStrokes.Insert(node);
                        }
                    }
                    if (!stroke.IsDone())
                    {
                        ++pendingStrokes;
                        if (stroke.IsVisible())
                        {
                            ++pendingVisibleStrokes;
                        }
                    }
                }
                
                if (pendingVisibleStrokes == 0)
                {
                    // consider m_unrenderedStrokes.First.Value.IsVisibleForPlayback
                    // m_prepopedStrokeCount++;
                    Debug.Log("Node is interpolating");
                    Debug.LogFormat("currentTimeMs is {0}", currentTimeMs);

                    var deltaTime = (float)(currentTimeMs - m_lastStrokeTailTimeMs) / (m_nextStrokeHeadTimeMs - m_lastStrokeTailTimeMs);
                    Debug.LogFormat("Finish interpolating at fraction {0}", deltaTime);
                    
                    // Head Object
                    var m_oculusAvatar = PlayBackObject.m_Instance.GetAvatar();
                    var rOculusAvatar = m_oculusAvatar.gameObject;
                    var HeadPos = Vector3.Lerp(m_lastHeadPosition, m_nextHeadPosition, deltaTime);
                    var HeadOrient = Quaternion.Lerp(m_lastHeadOrient, m_nextHeadOrient, deltaTime);
                    var xf_GS_avatar = Coords.CanvasPose * TrTransform.TR(HeadPos, HeadOrient);
                    xf_GS_avatar.scale = rOculusAvatar.transform.GetUniformScale();
                    Coords.AsGlobal[rOculusAvatar.transform] = xf_GS_avatar;

                    // Indicator Object
                    var m_strokeIndicator = PlayBackObject.m_Instance.GetIndicator();
                    var rStrokeIndicator = m_strokeIndicator.gameObject;
                    var IndicatorPos = Vector3.Lerp(m_lastIndicatorPosition, m_nextIndicatorPosition, deltaTime);
                    var IndicatorOrient = Quaternion.Lerp(m_lastIndicatorOrient, m_nextIndicatorOrient, deltaTime);
                    var xf_GS_indicator = Coords.CanvasPose * TrTransform.TR(IndicatorPos, IndicatorOrient);
                    xf_GS_indicator.scale = rStrokeIndicator.transform.GetUniformScale();
                    Coords.AsGlobal[rStrokeIndicator.transform] = xf_GS_indicator;

                    // Controller Object
                    var m_vrcontroller = PlayBackObject.m_Instance.GetController();
                    var rVrController = m_vrcontroller.gameObject;
                    var ControllerPos = Vector3.Lerp(m_lastControllerPosition, m_nextControllerPosition, deltaTime);
                    var ControllerOrient = Quaternion.Lerp(m_lastControllerOrient, m_nextControllerOrient, deltaTime);
                    var xf_GS_controller = Coords.CanvasPose * TrTransform.TR(ControllerPos, ControllerOrient);
                    xf_GS_controller.scale = rVrController.transform.GetUniformScale();
                    Coords.AsGlobal[rVrController.transform] = xf_GS_controller;

                }
                // check for pointer underrun
                int underrun = 0;
                foreach (var obj in m_unrenderedStrokes)
                {
                    if (!obj.IsVisibleForPlayback)
                    {
                        continue;
                    }
                    if (obj.HeadTimestampMs <= currentTimeMs)
                    {
                        ++underrun;
                    }
                    else
                    {
                        break;
                    }
                }
                m_maxPointerUnderrun = Mathf.Max(m_maxPointerUnderrun, underrun);
            }
            
            Debug.Assert(
                m_renderedStrokes.Count + pendingStrokes + m_unrenderedStrokes.Count == m_strokeCount);
            m_lastTimeMs = currentTimeMs;
            Debug.LogFormat("ScenePlayBack pending stroke is {0}", pendingStrokes);
            Debug.LogFormat("ScenePlayBack m_unrenderedStrokes stroke is {0}", m_unrenderedStrokes.Count);
            return !(m_unrenderedStrokes.Count == 0 && pendingStrokes == 0);
        }

        public void AddStroke(Stroke stroke)
        {
            // We expect call when user has completed stroke, so add to rendered list.  List
            // is sorted by end time and we expect new node to land at the head.
            m_renderedStrokes.Insert(stroke.m_PlaybackNode);
            ++m_strokeCount;
        }

        public void RemoveStroke(Stroke stroke)
        {
            // Only allowed for strokes in rendered or unrendered list.  In current use from ClearRedo,
            // it will always be unrendered.
            Debug.Assert(stroke.m_PlaybackNode.List != null);
            stroke.m_PlaybackNode.List.Remove(stroke.m_PlaybackNode); // O(1)
            --m_strokeCount;
        }

        public void QuickLoadRemaining() { App.Instance.CurrentSketchTime = float.MaxValue;}

        public Stroke GetNextVisibleStroke(SortedLinkedList<Stroke> strokeList)
        {
            var enumerator = strokeList.GetEnumerator();
            while(enumerator.MoveNext())
            {
                if (enumerator.Current.IsDrawForPlayback)
                {
                    return enumerator.Current;
                }
            }
            return null;
        }
    }

} // namespace TiltBrush
