using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;

namespace Veldrid.SceneGraph.Tests
{
    using IView = Veldrid.SceneGraph.Viewer.IView;
    using Scene = Veldrid.SceneGraph.Viewer.Scene;
    using View = Veldrid.SceneGraph.Viewer.View;

    [TestFixture]
    public class SceneShould
    {
        [Test]
        public void BeFoundByItsRootWhileAViewShowsIt()
        {
            var root = Group.Create();
            var view = View.Create(100, 100, 1000f);

            view.SetSceneData(root);

            Assert.That(Scene.GetScene(root), Is.Not.Null);
            Assert.That(Scene.GetScene(root).SceneData, Is.SameAs(root));
            GC.KeepAlive(view);
        }

        [Test]
        public void NotRegisterASceneWithoutData()
        {
            var view = View.Create(100, 100, 1000f);

            Assert.That(Scene.GetScene(null), Is.Null);
            GC.KeepAlive(view);
        }

        [Test]
        public void NotKeepTheSceneOfADroppedViewAlive()
        {
            var (scene, root) = ShowInAViewAndDropIt();

            CollectEverything();

            Assert.That(scene.IsAlive, Is.False);
            Assert.That(root.IsAlive, Is.False);
        }

        [Test]
        public void NotKeepTheRootAViewSwitchedAwayFromAlive()
        {
            var view = View.Create(100, 100, 1000f);
            var previousRoot = ShowThenSwitchToAnotherRoot(view);

            CollectEverything();

            Assert.That(previousRoot.IsAlive, Is.False);
            Assert.That(view.SceneData, Is.Not.Null);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static (WeakReference Scene, WeakReference Root) ShowInAViewAndDropIt()
        {
            var root = Group.Create();
            var view = View.Create(100, 100, 1000f);
            view.SetSceneData(root);
            return (new WeakReference(Scene.GetScene(root)), new WeakReference(root));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference ShowThenSwitchToAnotherRoot(IView view)
        {
            var previousRoot = Group.Create();
            view.SetSceneData(previousRoot);
            view.SetSceneData(Group.Create());
            return new WeakReference(previousRoot);
        }

        private static void CollectEverything()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }
}
