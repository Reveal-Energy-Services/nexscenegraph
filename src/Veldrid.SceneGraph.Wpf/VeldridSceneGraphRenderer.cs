using System;
using System.Diagnostics;
using System.Reactive.Subjects;
using System.Runtime.InteropServices;
using Veldrid.SceneGraph.RenderGraph;
using Veldrid.SceneGraph.InputAdapter;
using Veldrid.SceneGraph.Util;
using Veldrid.SceneGraph.Viewer;
using Veldrid.SceneGraph.Wpf.Element;
using Veldrid.Utilities;

namespace Veldrid.SceneGraph.Wpf
{
    public class VeldridSceneGraphRenderer : BaseRenderer, IDisposable
    {

        private ISubject<IEndFrameEvent> _endFrameEvents;
        private ISubject<IUiEventAdapter> _viewerInputEvents;

        private ISubject<float> _frameInfoSubject;
        public IObservable<float> FrameInfo => _frameInfoSubject;
        
        private IGroup _sceneData;
        public IGroup SceneData
        {
            get => _sceneData;
            set
            {
                _sceneData = value;
                if (null != _view)
                {
                    _view.SetSceneData(_sceneData);
                    CameraManipulator?.ViewAll(View);
                }
            }
        }

        private ICameraManipulator _cameraManipulator;

        public ICameraManipulator CameraManipulator
        {
            get => _cameraManipulator;
            set
            {
                _cameraManipulator = value;
                if (null != _view)
                {
                    _view.SetCameraManipulator(_cameraManipulator);
                    CameraManipulator?.ViewAll(View);
                }
            }
        }

        private IUiEventHandler _eventHandler;

        public IUiEventHandler EventHandler
        {
            get => _eventHandler;
            set
            {
                _eventHandler = value;
                if (null != _view)
                {
                    _view.AddInputEventHandler(_eventHandler);
                }
                
            }
        }

        private TextureSampleCount _fsaaCount;

        public TextureSampleCount FsaaCount
        {
            get => _fsaaCount;
            set
            {
                _fsaaCount = value;
                if (null != _graphicsDevice && null != _view)
                {
                    _sceneContext.SetMainSceneSampleCount(
                        _fsaaCount, 
                        _graphicsDevice, 
                        (uint)DisplaySettings.Instance(View).ScreenWidth, 
                        (uint)DisplaySettings.Instance(View).ScreenHeight);
                }
                
            }
        }

        private RgbaFloat _clearColor = RgbaFloat.Grey;

        public RgbaFloat ClearColor
        {
            get => _clearColor;
            set
            {
                _clearColor = value;
                if (null != _view)
                {
                    _view.Camera.SetClearColor(_clearColor);
                }
            }
        }
        
        
        public double DpiScale { get; set; }

        private Veldrid.SceneGraph.Viewer.IView _view;
        public Veldrid.SceneGraph.Viewer.IView View
        {
            get => _view;
        }
        
        public IObservable<IEndFrameEvent> EndFrameEvents => _endFrameEvents;
        public IObservable<IUiEventAdapter> ViewerInputEvents => _viewerInputEvents;
        
        private WpfInputStateSnapshot _inputState;
        
        private CommandList _commandList;
        private GraphicsDevice _graphicsDevice;
        
        private SceneContext _sceneContext;
        
        private Framebuffer _offscreenFB;
        
        private Texture _offscreenColor;

        // Our own COM reference to _offscreenColor's native texture, the copy source for the shared render target.
        private Vortice.Direct3D11.ID3D11Resource _offscreenColorResource;

        private Texture _offscreenDepth;

        private bool _disposed;

        private Fence _fence;
        
        private DisposeCollectorResourceFactory _factory;
        
        private event Action<GraphicsDevice, ResourceFactory> GraphicsDeviceOperations;
        private event Action<GraphicsDevice> GraphicsDeviceResize;
        
        private const uint NFramesInBuffer = 30;
        private ulong _frameCounter = 0;
        private ulong _globalFrameCounter = 0;
        private double _frameTimeAccumulator = 0.0;
        private double _fpsDrawTimeAccumulator = 0.0;
        private readonly double[] _frameTimeBuff = new double[NFramesInBuffer];
        private Stopwatch _stopwatch = null;
        private double _previousElapsed = 0;

        private bool _initialized;
        
        private readonly IUpdateVisitor _updateVisitor;
        
        public VeldridSceneGraphRenderer()
        {

            
            DpiScale = 1.0d;
            _initialized = false;
            _updateVisitor = UpdateVisitor.Create();
            _graphicsDevice = GraphicsDevice.CreateD3D11(new GraphicsDeviceOptions());
            
            if (_graphicsDevice.GetD3D11Info(out var backendInfo))
            {
                this.Renderer = new Element.D3D11(backendInfo.Device);
            }
            
            _factory = new DisposeCollectorResourceFactory(_graphicsDevice.ResourceFactory);
            _commandList = _graphicsDevice.ResourceFactory.CreateCommandList();
            _fence = _factory.CreateFence(false);
            _stopwatch = Stopwatch.StartNew();
            _previousElapsed = _stopwatch.Elapsed.TotalSeconds;
            _frameInfoSubject = new Subject<float>();
            
        }
        
        protected override void Attach()
        {
            if (Renderer == null)
                return;
            
            // Create Subjects
            _viewerInputEvents = new Subject<IUiEventAdapter>();
            _endFrameEvents = new Subject<IEndFrameEvent>();
            _inputState = new WpfInputStateSnapshot();
            
            
        }

        protected override void Detach()
        {
            _viewerInputEvents.OnCompleted();
            _endFrameEvents.OnCompleted();
            
            _graphicsDevice.WaitForIdle();
            _factory.DisposeCollector.DisposeAll();
        }

        /// <summary>
        /// Releases the graphics device and everything created on it. Rendering and input are no-ops afterwards.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            var element = Renderer;
            if (element != null)
            {
                Renderer = null; // Detach(): completes the event subjects and disposes the factory's resources
                element.Dispose();
            }
            else
            {
                _graphicsDevice.WaitForIdle();
                _factory.DisposeCollector.DisposeAll();
            }

            _offscreenColorResource?.Dispose();
            _offscreenColorResource = null;

            // These keep their own factories; every D3D11 resource holds a reference to its device.
            _sceneContext?.DestroyDeviceObjects();
            _view?.Camera.Renderer?.DestroyDeviceObjects();

            GraphicsDeviceOperations = null;
            GraphicsDeviceResize = null;

            RenderGroupState.ReleaseDevice(_graphicsDevice);
            _commandList.Dispose();

            _graphicsDevice.Dispose();
            _graphicsDevice = null;

            _frameInfoSubject.OnCompleted();
        }

        public void Initialize(uint width, uint height)
        {
            var view = Veldrid.SceneGraph.Viewer.View.Create(width, height, 1000.0f);
            
            DisplaySettings.Instance(view).SetScreenDistance(1000.0f);
            DisplaySettings.Instance(view).SetScreenWidth(width);
            DisplaySettings.Instance(view).SetScreenHeight(height);
            
            view.InputEvents = ViewerInputEvents;
            
            _sceneContext = new SceneContext(FsaaCount);
            _sceneContext.CreateDeviceObjects(_graphicsDevice, _factory);
            view.SceneContext = _sceneContext;

            if (null != _sceneData)
            {
                view.SetSceneData(_sceneData);
            }

            if (null != _cameraManipulator)
            {
                view.SetCameraManipulator(_cameraManipulator);
            }

            if (null != _eventHandler)
            {
                view.AddInputEventHandler(_eventHandler);
            }
            
            
            GraphicsDeviceOperations += view.Camera.Renderer.HandleOperation;
            GraphicsDeviceResize += view.Camera.Renderer.HandleResize;
            
            _view = view;
            ClearColor = _clearColor;
            FsaaCount = _fsaaCount;
            
            CameraManipulator?.ViewAll(View);

            _initialized = true;
        }

        public void SetCamera(ICamera camera)
        {
            GraphicsDeviceOperations -= _view.Camera.Renderer.HandleOperation;
            GraphicsDeviceResize -= _view.Camera.Renderer.HandleResize;
            
            _view.SetCamera(camera);
            
            GraphicsDeviceOperations += _view.Camera.Renderer.HandleOperation;
            GraphicsDeviceResize += _view.Camera.Renderer.HandleResize;
        }

        public void HandleInput(IUiEventAdapter eventAdapter)
        {
            _viewerInputEvents.OnNext(eventAdapter);
        }

        protected override void ResetCore(DrawEventArgs args)
        {
            
            if (args.RenderSize.Width == 0 || args.RenderSize.Height == 0) return;
            
            double dpiScale = 1.0;  // TODO: Check this is okay
            uint width = (uint)(args.RenderSize.Width < 0 ? 0 :  System.Math.Ceiling(args.RenderSize.Width * dpiScale));
            uint height = (uint)(args.RenderSize.Height < 0 ? 0 :  System.Math.Ceiling(args.RenderSize.Height * dpiScale));
            
            //_graphicsDevice.ResizeMainWindow((uint) width, (uint) height);
            
            if (!_initialized)
            {
                Initialize(width, height);
            }
            else
            {
                DisplaySettings.Instance(View).SetScreenWidth(width);
                DisplaySettings.Instance(View).SetScreenHeight(height);
            }
            
            
            //_sceneContext.RecreateWindowSizedResources(_graphicsDevice, _factory, width, height);
            
            var mainColorDesc = TextureDescription.Texture2D(
                width,
                height,
                1,
                1,
                PixelFormat.B8_G8_R8_A8_UNorm,
                TextureUsage.RenderTarget);

            _offscreenColor = _factory.CreateTexture(mainColorDesc);

            _offscreenColorResource?.Dispose();
            _offscreenColorResource = null;
            if (_graphicsDevice.GetD3D11Info(out var backendInfo))
            {
                var texturePointer = backendInfo.GetTexturePointer(_offscreenColor);
                Marshal.AddRef(texturePointer);
                _offscreenColorResource = new Vortice.Direct3D11.ID3D11Resource(texturePointer);
            }
            
            _offscreenDepth = _factory.CreateTexture(TextureDescription.Texture2D(
                width, 
                height, 
                1, 
                1, 
                PixelFormat.D24_UNorm_S8_UInt, 
                TextureUsage.DepthStencil));
            
            _offscreenFB = _factory.CreateFramebuffer(new FramebufferDescription(_offscreenDepth, _offscreenColor));

            var od = _offscreenFB.OutputDescription;
            
            if (null != _view)
            {
                _sceneContext.SetOutputFramebufffer(_offscreenFB);
            }
            
            _sceneContext.RecreateWindowSizedResources(_graphicsDevice, _factory, width, height);
            
            GraphicsDeviceResize?.Invoke(_graphicsDevice);
        }
        
        protected virtual void Frame()
        {
            var newElapsed = _stopwatch.Elapsed.TotalSeconds;
            var deltaSeconds = (float) (newElapsed - _previousElapsed);
            //
            // Rudimentary FPS Calc
            // 
            {

                _frameTimeAccumulator -= _frameTimeBuff[_frameCounter];
                _frameTimeBuff[_frameCounter] = deltaSeconds;
                _frameTimeAccumulator += deltaSeconds;

                _fpsDrawTimeAccumulator += deltaSeconds;
                if (_fpsDrawTimeAccumulator > 0.03333)
                {
                    var avgFps = (float)(NFramesInBuffer/_frameTimeAccumulator);
                
                    _frameInfoSubject.OnNext(avgFps);
                    _fpsDrawTimeAccumulator = 0.0;
                }
                
                // RingBuffer
                if (_frameCounter == NFramesInBuffer - 1)
                {
                    _frameCounter = 0;
                    
                }
                else
                {
                    _frameCounter++;
                }
            }
            
            _globalFrameCounter++;
            _previousElapsed = newElapsed;

            if (null == _graphicsDevice) return;
            
            RenderingTraversal();

            _endFrameEvents.OnNext(new EndFrameEvent(deltaSeconds));
            
        }
        
        private void RenderingTraversal()
        {
            GraphicsDeviceOperations?.Invoke(_graphicsDevice, _factory);
        }

        public override void RenderCore(DrawEventArgs args)
        {
            if (_disposed) return;

            if (null != _view && null != _view.SceneContext && null != _view.SceneContext.OutputFramebuffer)
            {
                Frame();

                // Copy Render Target
                if (_offscreenColorResource != null)
                    Renderer.CopyToRenderTarget(_offscreenColorResource);
            }
        }

        public void Resize(IResizedEvent resizeEvent)
        {
            _view.Camera.Resize(resizeEvent.Width, resizeEvent.Height, ResizeMask.ResizeDefault | ResizeMask.ResizeProjectionMatrix);
            
        }
    }
}