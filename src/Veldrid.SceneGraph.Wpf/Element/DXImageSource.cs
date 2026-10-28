using System;
using System.Windows;
using System.Windows.Interop;
using Vortice.Direct3D11;
using Vortice.Direct3D9;

namespace Veldrid.SceneGraph.Wpf.Element
{
	public class DXImageSource : D3DImage, IDisposable
	{
        /// <summary>
        ///
        /// </summary>
		public DXImageSource()
		{
			StartD3D9();
		}

		~DXImageSource() { Dispose(false); }

        /// <summary>
        ///
        /// </summary>
		public void Dispose() { Dispose(true); }

        /// <summary>
        ///
        /// </summary>
        /// <param name="disposing"></param>
		protected void Dispose(bool disposing)
		{
			if (IsDisposed)
				return;

			if (disposing)
			{
				// Let go of D3DImage's reference to the surface before releasing the texture.
				if (m_backBuffer != null)
					SetBackBuffer((IDirect3DTexture9)null);
				GC.SuppressFinalize(this);
			}

			EndD3D9();
			m_isDisposed = true;
		}

        /// <summary>
        ///
        /// </summary>
		public bool IsDisposed { get { return m_isDisposed; } }

        /// <summary>
        ///
        /// </summary>
		public void Invalidate()
		{
			if (IsDisposed)
				throw new ObjectDisposedException(GetType().Name);

			if (m_backBuffer != null)
			{
				Lock();
				AddDirtyRect(new Int32Rect(0, 0, base.PixelWidth, base.PixelHeight));
				Unlock();
			}
		}

        /// <summary>
        /// Displays a D3D11 texture created with ResourceOptionFlags.Shared.
        /// </summary>
        /// <param name="texture"></param>
		public void SetBackBuffer(ID3D11Texture2D texture) { SetBackBuffer(s_d3d9.Device.GetSharedD3D9(texture)); }

        /// <summary>
        /// Displays <paramref name="texture"/>, taking ownership of it; the previous back buffer is released.
        /// </summary>
        /// <param name="texture"></param>
		private void SetBackBuffer(IDirect3DTexture9 texture)
		{
			if (IsDisposed)
				throw new ObjectDisposedException(GetType().Name);

			var toDelete = m_backBuffer != texture ? m_backBuffer : null;
			m_backBuffer = texture;

			try
			{
				if (texture != null)
				{
					using (var surface = texture.GetSurfaceLevel(0))
					{
						Lock();
						SetBackBuffer(D3DResourceType.IDirect3DSurface9, surface.NativePointer, true);
						AddDirtyRect(new Int32Rect(0, 0, base.PixelWidth, base.PixelHeight));
						Unlock();
					}
				}
				else
				{
					// No dirty rect: D3DImage throws on AddDirtyRect without a back buffer.
					Lock();
					SetBackBuffer(D3DResourceType.IDirect3DSurface9, IntPtr.Zero, true);
					Unlock();
				}
			}
			finally
			{
				toDelete?.Dispose();
			}
		}

        /// <summary>
        ///
        /// </summary>
		private static void StartD3D9()
		{
			if (s_activeClients == 0)
				s_d3d9 = new D3D9();
			s_activeClients++;
		}

        /// <summary>
        ///
        /// </summary>
		private static void EndD3D9()
		{
			s_activeClients--;
			if (s_activeClients == 0)
			{
				s_d3d9.Dispose();
				s_d3d9 = null;
			}
		}

		private static int s_activeClients;
		private static D3D9 s_d3d9;

        private bool m_isDisposed;
        private IDirect3DTexture9 m_backBuffer;
	}
}
