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

namespace TiltBrush
{

    public class SketchSurfaceTool : BaseTool
    {
        public GameObject m_FrontSide;
        public GameObject m_BackSide;
        public Color m_FrontSideColor;
        public Color m_BackSideColor;
        public float m_AdjustBrushSizeScalar;

        override public void Init()
        {
            base.Init();

            Renderer rFrontRenderer = m_FrontSide.GetComponent<Renderer>();
            rFrontRenderer.material.color = m_FrontSideColor;

            Renderer rBackRenderer = m_BackSide.GetComponent<Renderer>();
            rBackRenderer.material.color = m_BackSideColor;
        }

        public override bool ShouldShowPointer()
        {
            return true;
        }

        override public void EnableTool(bool bEnable)
        {
            base.EnableTool(bEnable);
            if (!bEnable)
            {
                Debug.Log("EnableLine is disabled in EnableTool in SketchSurfaceTool");
                PointerManager.m_Instance.EnableLine(false);
            }
        }

        override public void UpdateTool()
        {
            base.UpdateTool();

            bool bEnableLine = InputManager.m_Instance.GetCommand(InputManager.SketchCommands.Activate);
            bEnableLine = bEnableLine && !m_EatInput && m_AllowDrawing && m_SketchSurface.IsSurfaceDrawable();
            Debug.Log("m_EatInput is " + m_EatInput);
            Debug.Log("m_AllowDrawing is " + m_AllowDrawing);
            Debug.Log("m_SketchSurface.IsSurfaceDrawable() is " + m_SketchSurface.IsSurfaceDrawable());
            Debug.Log("EnableLine in UpdateTool in SketchSurfaceTool is " + bEnableLine);
            PointerManager.m_Instance.EnableLine(bEnableLine);
            PointerManager.m_Instance.PointerPressure = 1.0f;
        }

        override public void UpdateSize(float fAdjustAmount)
        {
            PointerManager.m_Instance.AdjustAllPointersBrushSize01(m_AdjustBrushSizeScalar * fAdjustAmount);
            PointerManager.m_Instance.MarkAllBrushSizeUsed();
        }

        override public void BacksideActive(bool bActive)
        {
            m_FrontSide.SetActive(!bActive);
            m_BackSide.SetActive(bActive);
        }
    }
} // namespace TiltBrush
