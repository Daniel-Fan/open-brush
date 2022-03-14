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

using UnityEngine;
using System.Collections.Generic;

namespace TiltBrush
{

    // Binds stroke data to a pointer for playback.
    public abstract class StrokePlayback
    {
        private static HashSet<System.Guid> sm_BrushGuidWarnings = new HashSet<System.Guid>();

        protected Stroke m_stroke;
        private StrokeIndicator m_strokeIndicator;
        private OculusAvatar m_oculusAvatar;
        private VrControllers m_vrcontroller;
        private PointerScript m_pointer;
        private int m_nextControlPoint;
        private bool m_isDone = true;
        private bool m_isVisible;

        // Using "Init" here so usage can avoid per-stroke heap activity.
        public void BaseInit(Stroke stroke,
                             PointerScript pointer,
                             CanvasScript canvas,
                             StrokeIndicator indicator,
                             OculusAvatar avatar,
                             VrControllers controller)
        {
            UnityEngine.Debug.Log("Check stroke m_type" + (stroke.m_Type != Stroke.Type.NotCreated));
            if (stroke.m_Type != Stroke.Type.NotCreated)
            {
                UnityEngine.Debug.LogWarningFormat("Unexpected: playback stroke type {0}", stroke.m_Type);
                stroke.Uncreate();
            }
            m_pointer = pointer;
            m_stroke = stroke;
            m_strokeIndicator = indicator;
            m_oculusAvatar = avatar;
            m_vrcontroller = controller;
            m_nextControlPoint = 0;
            m_isDone = false;
            m_isVisible = stroke.IsDrawForPlayback;

            // We make the BeginLine call, even though the stroke time may be in the future.
            // Harmless with current brushes since no geometry is generated until UpdateLinePosition.
            // We can move code to Update() if this changes.            
            UnityEngine.Debug.Log("finish the variable assignment, starting to begin line from memory");
            m_stroke.m_Object = m_pointer.BeginLineFromMemory(m_stroke, canvas);
            UnityEngine.Debug.Log("after begin line from memory");
            m_stroke.m_Type = Stroke.Type.BrushStroke;
            // m_strokeIndicator = StrokeIndicator.Create(); // canvas.transform

            if (m_stroke.m_Object == null)
            {
                if (!sm_BrushGuidWarnings.Contains(m_stroke.m_BrushGuid))
                {
                    OutputWindowScript.m_Instance.AddNewLine("Brush not found!  Guid: " + m_stroke.m_BrushGuid);
                    sm_BrushGuidWarnings.Add(m_stroke.m_BrushGuid);
                }
                m_isDone = true;
            }
        }

        public virtual void ClearPlayback()
        {
            if (!m_isDone)
            {
                m_pointer.EndLineFromMemory(m_stroke);
                //UnityEngine.Debug.Log("Starting Clear PlayBack Object");
                //PlayBackObject.m_Instance.ClearPlayBack();
                //UnityEngine.Debug.Log("Finish Clear PlayBack Object");
                m_isDone = true;
            }
        }

        public bool IsDone()
        {
            return m_isDone;
        }

        public bool IsVisible()
        {
            return m_isVisible;
        }

        public uint GetHeadTime
        {
            get
            {
                return m_stroke.m_ControlPoints[0].m_TimestampMs;
            }
        }

        protected abstract bool IsControlPointReady(PointerManager.ControlPoint controlPoint, double TotalCurrentPauedTimeMs);

        // Continue drawing stroke for this frame.
        public void Update(double TotalCurrentPauedTimeMs)
        {
            if (m_isDone) { return; }

            UnityEngine.Debug.Log("Assign the basic variables");
            var rPointerScript = m_pointer;
            var rPointerObject = m_pointer.gameObject;
            var rStrokeIndicator = m_strokeIndicator.gameObject;
            UnityEngine.Debug.Log("Assign the Avator variables");
            var rOculusAvatar = m_oculusAvatar.gameObject;
            UnityEngine.Debug.Log("Assign the controller variables");
            var rVrController = m_vrcontroller.gameObject;

            bool needMeshUpdate = false;
            bool needPointerUpdate = false;
            bool strokeFinished = false;
            UnityEngine.Debug.Log("Before updating the control point");
            var lastCp = new PointerManager.ControlPoint();
            var lastCphead = new PointerManager.ControlPoint();
            OverlayManager.m_Instance.UpdateProgress(SketchMemoryScript.m_Instance.GetDrawnPercent());

            UnityEngine.Debug.Log("Inside strokePlayBack update()");
            RdpStrokeSimplifier simplifier = QualityControls.m_Instance.StrokeSimplifier;
            if (simplifier.Level > 0.0f)
            {
                simplifier.CalculatePointsToDrop(m_stroke, m_pointer.CurrentBrushScript);
            }

            while (true)
            {
                if (m_nextControlPoint >= m_stroke.m_ControlPoints.Length)
                {
                    needMeshUpdate = true; // Is this really necessary?
                    strokeFinished = true;
                    break;
                }
                var cp = m_stroke.m_ControlPoints[m_nextControlPoint];
                lastCphead = cp;
                if (!IsControlPointReady(cp, TotalCurrentPauedTimeMs))
                {
                    break;
                }
                UnityEngine.Debug.Log("Inside strokePlayBack update while loop");
                UnityEngine.Debug.LogFormat("Unexpected: playback pointerscript {0}", rPointerScript);
                UnityEngine.Debug.LogFormat("Unexpected: playback stroke control point {0}", cp);

                if (!m_stroke.m_ControlPointsToDrop[m_nextControlPoint])
                {
                    rPointerScript.UpdateLineFromControlPoint(cp);
                    needMeshUpdate = true;
                    lastCp = cp;
                    needPointerUpdate = true;
                }
                ++m_nextControlPoint;
            }

            if (needMeshUpdate)
            {
                rPointerScript.UpdateLineVisuals();
            }

            

            if (needPointerUpdate)
            {
                // This is only really done for visual reasons
                if (m_stroke.IsDrawForPlayback)
                {
                    var xf_GS_indicator = Coords.CanvasPose * TrTransform.TR(lastCp.m_Pos, lastCp.m_Orient);
                    xf_GS_indicator.scale = rStrokeIndicator.transform.GetUniformScale();
                    Coords.AsGlobal[rStrokeIndicator.transform] = xf_GS_indicator;


                    var xf_GS_avatar = Coords.CanvasPose * TrTransform.TR(lastCphead.m_HeadPos, lastCphead.m_HeadOrient);
                    xf_GS_avatar.scale = rOculusAvatar.transform.GetUniformScale();
                    Coords.AsGlobal[rOculusAvatar.transform] = xf_GS_avatar;

                    var xf_GS_controller = Coords.CanvasPose * TrTransform.TR(lastCp.m_ControllerPos, lastCp.m_ControllerOrient);
                    xf_GS_controller.scale = rVrController.transform.GetUniformScale();
                    Coords.AsGlobal[rVrController.transform] = xf_GS_controller;
                }

                // This is only really done for visual reasons
                var xf_GS = Coords.CanvasPose * TrTransform.TR(lastCp.m_Pos, lastCp.m_Orient);
                xf_GS.scale = rPointerObject.transform.GetUniformScale();
                Coords.AsGlobal[rPointerObject.transform] = xf_GS;
                rPointerScript.SetPressure(lastCp.m_Pressure);
            }

            if (strokeFinished)
            {
                rPointerScript.EndLineFromMemory(m_stroke);
                m_isDone = true;
            }
        }
    }

} // namespace TiltBrush
