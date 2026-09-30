using System.Collections.Generic;
using UnityEngine;

namespace SDG.Unturned
{
    /// <summary>Resolve project shader references before duplicate shader names from Steam bundles.</summary>
    public sealed class UniversalShaderCatalog : ScriptableObject
    {
        public Shader[] shaders;
        private static Dictionary<string, Shader> projectShaders;

        public static Shader Find(string name)
        {
            if (projectShaders == null)
            {
                projectShaders = new Dictionary<string, Shader>(System.StringComparer.Ordinal);
                var catalog = Resources.Load<UniversalShaderCatalog>("UnturnedUniversalShaders");
                if (catalog != null && catalog.shaders != null)
                    foreach (var shader in catalog.shaders)
                        if (shader != null) projectShaders[shader.name] = shader;
            }
            return projectShaders.TryGetValue(name, out var replacement) ? replacement : Shader.Find(name);
        }
    }
}
