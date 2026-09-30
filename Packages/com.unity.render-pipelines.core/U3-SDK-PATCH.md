# Unity 6.7 beta CLI integration fix

This is Unity's embedded SRP Core 17.7.0 package, copied from the installed 6000.7.0b2 package.
The CLI configuration change is the `Unity.Pipeline.Attributes` assembly reference in
`Editor/CLI/Unity.RenderPipelines.Core.Editor.CLI.asmdef`. Its RenderGraph CLI adapter already uses
`CliCommandAttribute` and `CliArgAttribute`, but the bundled assembly definition omits their
assembly reference when the official Pipeline 0.8.0-exp.1 package is installed.

The reference enables the official RenderGraph CLI commands without deleting their implementation
or modifying transient PackageCache contents. All upstream source and license notices are preserved.
Remove this embedded override when an editor/package update provides the reference upstream.

The LightmapIndirectIntegration compute shader excludes glcore: its storage buffer count exceeds that backend limit in 6000.7.0b2. D3D11, Vulkan, and Metal targets remain enabled. Windows players target DirectX 12 and Vulkan exclusively. The compatibility pragma `d3d11` includes both DirectX APIs; the player API list still excludes DirectX 11.

The RadeonRays BVH headers `intersector_common.hlsl` and `bvh2il.hlsl` use explicit
unsigned 32-bit masks. Their previous complement literal widened to a negative
integer before conversion to `uint`, producing DXC warnings while importing probe shaders.

`Editor/Lighting/ProbeVolume/DynamicGI/DynamicGISkyOcclusion.urtshader` disables
optimization for the DirectX compute backend and emits hardware ray-tracing debug symbols
using the current `enable_debug_symbols` directive. The bundled DXC optimizer otherwise
emits an invalid `i10` DXIL integer for the hardware ray-generation shader. Both imported
subassets now report zero messages. It affects an editor-only probe kernel; optional
Dynamic GI baking performance has not been qualified. Remove this workaround when Unity
fixes the compiler rather than disabling shader optimization project-wide.

The player graphics-settings stripper excludes test assemblies referencing NUnit during player
builds. Test fixtures contain deliberately invalid stripper constructors; they are not
production strippers. Normal editor/test discovery and its constructor validation are retained.
