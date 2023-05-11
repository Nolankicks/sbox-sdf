﻿using System;
using System.Text.Json.Serialization;

namespace Sandbox.Sdf
{
    /// <summary>
    /// Controls the appearance and physical properties of a layer in a <see cref="Sdf2DWorld"/>.
    /// </summary>
    [GameResource("SDF 2D Material", "sdflayer", $"Material used by {nameof(Sdf2DWorld)}", Icon = "brush")]
    public class Sdf2DMaterial : GameResource
    {
        private static char[] SplitChars { get; } = new[] { ' ' };

        /// <summary>
        /// Tags that physics shapes with this material should have, separated by spaces.
        /// If empty, no physics shapes will be created.
        /// </summary>
        [Editor( "tags" )]
        public string CollisionTags { get; set; } = "solid";

        [HideInEditor, JsonIgnore]
        public string[] SplitCollisionTags =>
            CollisionTags?.Split( SplitChars, StringSplitOptions.RemoveEmptyEntries ) ?? Array.Empty<string>();

        /// <summary>
        /// How wide this layer is in the z-axis.
        /// </summary>
        public float Depth { get; set; } = 64f;

        /// <summary>
        /// Material used by the front face of this layer.
        /// </summary>
        public Material FrontFaceMaterial { get; set; }

        /// <summary>
        /// Material used by the back face of this layer.
        /// </summary>
        public Material BackFaceMaterial { get; set; }

        /// <summary>
        /// Material used by the cut face connecting the front and
        /// back of this layer.
        /// </summary>
        public Material CutFaceMaterial { get; set; }
    }
}
