////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
using SDG.Unturned;
using System;
using UnityEngine;

namespace SDG.Framework.Rendering
{
	public delegate void GLRenderHandler();

	public class GLRenderer : MonoBehaviour
	{
		public readonly System.Collections.Generic.List<GraphGeometry.Draw> draws = new System.Collections.Generic.List<GraphGeometry.Draw>();
        private void OnDestroy() { foreach (var draw in draws) Destroy(draw.mesh); draws.Clear(); }
		public static event GLRenderHandler render;
		public static event GLRenderHandler OnGameRender;

		public int RecordGeometry()
		{
			GraphGeometry.StartRecording(draws);
			try
			{

			bool shouldRenderAny = false;
			bool shouldInvokeRenderEvent = false;
			bool shouldInvokeGameRenderEvent = false;
			bool shouldRenderGizmos = false;

			if (Level.isEditor)
			{
				shouldInvokeRenderEvent = render != null;

				if (EditorUI.window == null || !EditorUI.window.isEnabled)
				{
					shouldInvokeRenderEvent = false;
				}

				shouldRenderAny |= shouldInvokeRenderEvent;
			}
			else
			{
				shouldInvokeGameRenderEvent = OnGameRender != null;

				if (PlayerUI.window == null || !PlayerUI.window.isEnabled)
				{
					shouldInvokeGameRenderEvent = false;
				}

				shouldRenderAny |= shouldInvokeGameRenderEvent;
			}

			shouldRenderGizmos = RuntimeGizmos.Get().HasQueuedElements;
			shouldRenderAny |= shouldRenderGizmos;

			if (shouldRenderAny)
			{


				if (shouldInvokeRenderEvent)
				{
					GraphGeometry.PushMatrix();
					try
					{
						render();
					}
					catch (Exception e)
					{
						UnturnedLog.exception(e);
					}
					GraphGeometry.PopMatrix();
				}

				if (shouldInvokeGameRenderEvent)
				{
					GraphGeometry.PushMatrix();
					try
					{
						OnGameRender();
					}
					catch (Exception e)
					{
						UnturnedLog.exception(e);
					}
					GraphGeometry.PopMatrix();
				}

				if (shouldRenderGizmos)
				{
					GraphGeometry.PushMatrix();
					try
					{
						RuntimeGizmos.Get().Render();
					}
					catch (Exception e)
					{
						UnturnedLog.exception(e);
					}
					GraphGeometry.PopMatrix();
				}


			}
			return GraphGeometry.FinishRecording();
			}
			finally { GraphGeometry.FinishRecording(); }
		}
	}
}
