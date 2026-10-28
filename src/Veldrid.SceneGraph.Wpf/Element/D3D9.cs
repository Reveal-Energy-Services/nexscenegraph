using System;
using System.Runtime.InteropServices;
using Vortice.Direct3D9;

namespace Veldrid.SceneGraph.Wpf.Element
{
    /// <summary>
    /// The D3D9Ex device that WPF's D3DImage needs to open D3D11 shared render targets.
    /// </summary>
    internal sealed class D3D9 : IDisposable
    {
        public D3D9()
        {
            m_context = Vortice.Direct3D9.D3D9.Direct3DCreate9Ex();

            var presentparams = new PresentParameters
            {
                Windowed = true,
                SwapEffect = SwapEffect.Discard,
                DeviceWindowHandle = GetDesktopWindow(),
                PresentationInterval = PresentInterval.Default
            };

            m_device = m_context.CreateDeviceEx(0, DeviceType.Hardware, IntPtr.Zero,
                CreateFlags.HardwareVertexProcessing | CreateFlags.Multithreaded | CreateFlags.FpuPreserve,
                presentparams);
        }

        public void Dispose()
        {
            m_device?.Dispose();
            m_device = null;
            m_context?.Dispose();
            m_context = null;
        }

        public IDirect3DDevice9Ex Device => m_device ?? throw new ObjectDisposedException(nameof(D3D9));

        [DllImport("user32.dll", SetLastError = false)]
        static extern IntPtr GetDesktopWindow();

        private IDirect3D9Ex m_context;
        private IDirect3DDevice9Ex m_device;
    }
}
