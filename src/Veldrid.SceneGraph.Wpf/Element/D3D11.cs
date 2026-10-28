using System;
using System.Runtime.InteropServices;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace Veldrid.SceneGraph.Wpf.Element
{
    /// <summary>
    /// A shared render target on an existing D3D11 device, displayed in WPF through <see cref="DXImageSource"/>.
    /// </summary>
    public class D3D11 : D3D
    {
        /// <summary>
        /// Wraps an existing native ID3D11Device. Takes its own COM reference, so disposing this object
        /// releases only that reference and never the owner's.
        /// </summary>
        /// <param name="devicePointer">Native ID3D11Device pointer, e.g. Veldrid's BackendInfoD3D11.Device.</param>
        public D3D11(IntPtr devicePointer)
        {
            if (devicePointer == IntPtr.Zero)
                throw new ArgumentNullException(nameof(devicePointer));

            Marshal.AddRef(devicePointer);
            m_device = new ID3D11Device(devicePointer);
            m_context = m_device.ImmediateContext;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="disposing"></param>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            Set(ref m_renderTarget, null);
            Set(ref m_context, null);
            Set(ref m_device, null);
        }

        /// <summary>
        ///
        /// </summary>
        public ID3D11Device Device => m_device ?? throw new ObjectDisposedException(nameof(D3D11));

        /// <summary>
        ///
        /// </summary>
        public bool IsDisposed => m_device == null;

        /// <summary>
        ///
        /// </summary>
        /// <param name="dximage"></param>
        public override void SetBackBuffer(DXImageSource dximage) { dximage.SetBackBuffer(RenderTarget); }

        /// <summary>
        ///
        /// </summary>
        /// <param name="w"></param>
        /// <param name="h"></param>
        public override void Reset(int w, int h)
        {
            if (w < 1)
                throw new ArgumentOutOfRangeException("w");
            if (h < 1)
                throw new ArgumentOutOfRangeException("h");

            var desc = new Texture2DDescription
            {
                BindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource,
                Format = Format.B8G8R8A8_UNorm,
                Width = w,
                Height = h,
                MipLevels = 1,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                MiscFlags = RenderTargetOptionFlags,
                CPUAccessFlags = CpuAccessFlags.None,
                ArraySize = 1
            };

            Set(ref m_renderTarget, Device.CreateTexture2D(desc));
        }

        /// <summary>
        /// Copies <paramref name="source"/> (same size and format) into the shared render target.
        /// </summary>
        public void CopyToRenderTarget(ID3D11Resource source)
        {
            var target = RenderTarget;
            m_context.CopyResource(target, source);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="args"></param>
        public override void EndRender(DrawEventArgs args)
        {
            // KLUDGE - this line crashes multi-window demo - SEAN
            m_context?.Flush();
        }

        /// <summary>
        ///
        /// </summary>
        public ID3D11Texture2D RenderTarget
        {
            get
            {
                if (m_renderTarget == null)
                    Reset(1, 1);
                return m_renderTarget;
            }
        }

        /// <summary>
        ///
        /// </summary>
        public ResourceOptionFlags RenderTargetOptionFlags
        {
            get { return m_renderTargetOptionFlags; }
            set
            {
                if (value == m_renderTargetOptionFlags) return;
                m_renderTargetOptionFlags = value;
                OnPropertyChanged("RenderTargetOptionFlags");
            }
        }

        #region Fields

        /// <summary>
        /// must be Shared to be displayed in a D3DImage
        /// </summary>
        private ResourceOptionFlags m_renderTargetOptionFlags = ResourceOptionFlags.Shared;

        private ID3D11Device m_device;
        private ID3D11DeviceContext m_context;
        private ID3D11Texture2D m_renderTarget;

        #endregion
    }
}
