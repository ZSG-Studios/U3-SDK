# Unity 6.7 beta decal output fix

This is the installed URP 17.7.0 package, embedded without changing its version or assembly identities.
Upstream source and license notices are preserved.

The shader patch initializes `GBufferFragOutput` in
`Editor/ShaderGraph/Includes/ShaderPassDecal.hlsl` before populating the decal outputs.
The bundled shader omits optional MRT fields and produces uninitialized-output
warnings on DirectX 12 and Vulkan. Initialization makes these outputs deterministic. `UniversalDecalSubTarget.cs` also masks writes to optional MRTs, preserving the scene depth, shadowmask and rendering layers.

`DecalRendererFeature.cs` creates the DBuffer clear material only when DBuffer is selected.
`Runtime/Decal/DBuffer/DBufferClear.shader` includes a no-keyword single-target variant.
The renderer resources load this shader even for Screen Space/GBuffer renderers;
without that variant, URP strips all fragment programs and the player reports an
unsupported shader while loading its resources. Always Included Shaders alone does
not prevent the native scriptable stripper from removing these programs.
Remove this override when an editor/package update supplies the fix upstream.

The depth-copy test fixture checks the pipeline saved before its base setup installs
a temporary RenderGraph test pipeline. Missing URP shader settings fail assertions
instead of returning without testing. The Surface Cache GI shader-feature fixture
temporarily disables incompatible static batching, requests raw mesh buffers, and restores
both project settings.

UniversalAdditionalCameraData disposes nonserialized history in Editor OnDisable before
domain reload recreates its owner. Persistent Scene View cameras otherwise leak SSR history
textures across entering/leaving Play mode. The focused resource-leak test now passes.

The three bundled Autodesk Interactive shadergraph importer metadata files were reserialized through Unity. Their script/shader GUIDs and main shader file IDs are retained; ScriptedImporter metadata now uses version 2 and the current internal ID table rather than the old version-1 defaults.
