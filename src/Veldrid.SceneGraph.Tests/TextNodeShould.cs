using System.Linq;
using NUnit.Framework;
using Veldrid.SceneGraph.AssetPrimitives;
using Veldrid.SceneGraph.Text;

namespace Veldrid.SceneGraph.Tests
{
    [TestFixture]
    public class TextNodeShould
    {
        private static readonly RgbaByte White = new RgbaByte(255, 255, 255, 255);
        private static readonly RgbaByte Black = new RgbaByte(0, 0, 0, 255);
        private static readonly RgbaByte Transparent = new RgbaByte(0, 0, 0, 0);

        [Test]
        public void RenderInkOnTransparentBackground()
        {
            var texture = Texture(TextNode.Create("Hello", new TextFont("Arial", 20), White, Transparent));

            Assert.That(InkPixels(texture), Is.GreaterThan(0));
            Assert.That(texture.TextureData[3], Is.EqualTo(0), "corner pixel should stay transparent");
        }

        [Test]
        public void GrowTextureWithFontSize()
        {
            var small = Texture(TextNode.Create("Hello", new TextFont("Arial", 10), White, Transparent));
            var large = Texture(TextNode.Create("Hello", new TextFont("Arial", 40), White, Transparent));

            Assert.That(large.Width, Is.GreaterThan(small.Width));
            Assert.That(large.Height, Is.GreaterThan(small.Height));
        }

        [Test]
        public void ProduceMoreInkWithOutline()
        {
            var plain = Texture(TextNode.Create("Hello", new TextFont("Arial", 20, Bold: true), White, Transparent));
            var outlined = Texture(TextNode.Create("Hello", new TextFont("Arial", 20, Bold: true), White, Black, 2f,
                Transparent));

            Assert.That(InkPixels(outlined), Is.GreaterThan(InkPixels(plain)));
        }

        [Test]
        public void FallBackWhenFamilyIsMissing()
        {
            var texture = Texture(TextNode.Create("Hello", new TextFont("No Such Font Family", 20), White,
                Transparent));

            Assert.That(InkPixels(texture), Is.GreaterThan(0));
        }

        [Test]
        public void ProduceFullMipChain()
        {
            var texture = Texture(TextNode.Create("Hello", new TextFont("Arial", 20), White, Transparent));

            var expectedLevels = AssetProcessor.SkiaImageProcessor.ComputeMipLevels((int) texture.Width,
                (int) texture.Height);
            Assert.That(texture.MipLevels, Is.EqualTo((uint) expectedLevels));
        }

        private static ProcessedTexture Texture(ITextNode node) =>
            ((TextureBase) node.PipelineState.TextureList.Last()).ProcessedTexture;

        // Counts top-mip pixels with any alpha.
        private static int InkPixels(ProcessedTexture texture)
        {
            var count = 0;
            for (var i = 3; i < texture.Width * texture.Height * 4; i += 4)
                if (texture.TextureData[i] > 0)
                    count++;
            return count;
        }
    }
}
