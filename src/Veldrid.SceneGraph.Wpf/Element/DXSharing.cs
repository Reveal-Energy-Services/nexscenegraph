using System;
using Vortice.Direct3D11;
using Vortice.Direct3D9;
using Vortice.DXGI;

namespace Veldrid.SceneGraph.Wpf.Element
{
    internal static class DXSharing
    {
        /// <summary>
        /// D3D9 equivalent of a DXGI render-target format, or <see cref="Vortice.Direct3D9.Format.Unknown"/>.
        /// </summary>
        public static Vortice.Direct3D9.Format ToD3D9(this Vortice.DXGI.Format dxgiformat)
        {
            switch (dxgiformat)
            {
                case Vortice.DXGI.Format.B8G8R8A8_UNorm:
                case Vortice.DXGI.Format.R8G8B8A8_UNorm:
                    return Vortice.Direct3D9.Format.A8R8G8B8;
                case Vortice.DXGI.Format.R10G10B10A2_UNorm:
                    return Vortice.Direct3D9.Format.A2B10G10R10;
                case Vortice.DXGI.Format.R16G16B16A16_Float:
                    return Vortice.Direct3D9.Format.A16B16G16R16F;
                default:
                    return Vortice.Direct3D9.Format.Unknown;
            }
        }

        /// <summary>
        /// Opens a D3D11 texture created with <see cref="ResourceOptionFlags.Shared"/> as a D3D9 texture.
        /// The caller owns the returned texture.
        /// </summary>
        public static IDirect3DTexture9 GetSharedD3D9(this IDirect3DDevice9Ex device, ID3D11Texture2D renderTarget)
        {
            if (renderTarget == null)
                return null;

            var description = renderTarget.Description;
            if ((description.MiscFlags & ResourceOptionFlags.Shared) == 0)
                throw new ArgumentException("Texture must be created with ResourceOptionFlags.Shared");

            var format = description.Format.ToD3D9();
            if (format == Vortice.Direct3D9.Format.Unknown)
                throw new ArgumentException("Texture format is not compatible with OpenSharedResource");

            using (var resource = renderTarget.QueryInterface<IDXGIResource>())
            {
                IntPtr handle = resource.SharedHandle;
                if (handle == IntPtr.Zero)
                    throw new ArgumentNullException("Handle");

                return device.CreateTexture(description.Width, description.Height, 1, Vortice.Direct3D9.Usage.RenderTarget, format,
                    Pool.Default, ref handle);
            }
        }
    }
}
