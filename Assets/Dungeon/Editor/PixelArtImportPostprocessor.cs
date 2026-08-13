using UnityEditor;
using UnityEngine;

namespace Dungeon.Editor
{
    /// <summary>
    /// Forces pixel-art-correct import settings on every texture under <c>Assets/Art/</c>.
    /// </summary>
    /// <remarks>
    /// Unity's default texture import is actively wrong for pixel art: bilinear filtering blurs the
    /// pixel grid and DXT compression adds colour artefacts to the flat blocks that pixel art is made
    /// of. The result looks smeared and dirty at every zoom level, and — because the defaults are
    /// reapplied on reimport — hand-fixing an asset in the inspector lasts only until someone
    /// reimports it. Doing it here makes the rule structural instead of a habit.
    /// <para>
    /// <b>Pixels-per-unit is 64</b> to match the art's tile grid, so one 64x64 tile is exactly one
    /// world unit. Keep this in step with the tile size: if the grid changes, this constant and the
    /// camera's orthographic size both move together, and every prefab placed on integer coordinates
    /// silently shifts if only one of them is updated.
    /// </para>
    /// <para>
    /// <see cref="TextureImporter.spriteImportMode"/> is only set on a texture's <i>first</i> import.
    /// A sliced atlas (the terrain tileset) carries a hand-authored sprite sheet, and stamping
    /// <see cref="SpriteImportMode.Single"/> over it on every reimport would destroy that slicing.
    /// </para>
    /// </remarks>
    public sealed class PixelArtImportPostprocessor : AssetPostprocessor
    {
        /// <summary>Folder whose textures are treated as pixel art. Case-sensitive, forward slashes.</summary>
        private const string ArtRoot = "Assets/Art/";

        /// <summary>Pixels per world unit. Matches the 64x64 art grid: one tile is one unit.</summary>
        private const float PixelsPerUnit = 64f;

        /// <summary>
        /// Import-settings version. Bumping this makes Unity reimport every affected texture, which
        /// is the only way a change to the rules below reaches assets that are already in the project.
        /// </summary>
        /// <returns>The current version of this postprocessor's settings.</returns>
        public override uint GetVersion() => 1;

        /// <summary>
        /// Applies point filtering, uncompressed colour and the project's pixels-per-unit to any
        /// texture imported from under <see cref="ArtRoot"/>. Textures elsewhere are left alone.
        /// </summary>
        private void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').StartsWith(ArtRoot, System.StringComparison.Ordinal))
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;

            // Only claim the sprite mode on a first import, so manual atlas slicing survives.
            if (importer.importSettingsMissing)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
            }

            importer.filterMode = FilterMode.Point;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;

            TextureImporterPlatformSettings platform = importer.GetDefaultPlatformTextureSettings();
            platform.textureCompression = TextureImporterCompression.Uncompressed;
            platform.crunchedCompression = false;
            importer.SetPlatformTextureSettings(platform);
        }
    }
}
