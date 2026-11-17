//
// Copyright 2018-2021 Sean Spicer 
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//    http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//

using System;
using System.Runtime.CompilerServices;

namespace Veldrid.SceneGraph.Viewer
{
    internal class SceneSingleton
    {
        private static readonly Lazy<SceneSingleton> Lazy = new Lazy<SceneSingleton>(() => new SceneSingleton());

        private readonly ConditionalWeakTable<INode, Scene> _sceneCache = new ConditionalWeakTable<INode, Scene>();

        private SceneSingleton()
        {
        }

        public static SceneSingleton Instance => Lazy.Value;

        public void Add(Scene scene)
        {
            if (null != scene.SceneData)
                _sceneCache.TryAdd(scene.SceneData, scene);
        }

        public void Remove(Scene scene)
        {
            if (null != scene.SceneData
                && _sceneCache.TryGetValue(scene.SceneData, out var cached)
                && ReferenceEquals(cached, scene))
                _sceneCache.Remove(scene.SceneData);
        }

        public Scene GetScene(INode node)
        {
            return null != node && _sceneCache.TryGetValue(node, out var scene) ? scene : null;
        }
    }

    public class Scene
    {
        internal Scene()
        {
        }

        public INode SceneData { get; private set; }

        public static Scene GetScene(INode node)
        {
            return SceneSingleton.Instance.GetScene(node);
        }

        protected static Scene GetOrCreateScene(INode node)
        {
            var scene = GetScene(node);

            if (null != scene) return scene;

            scene = new Scene();
            scene.SetSceneData(node);

            return scene;
        }


        public void SetSceneData(INode node)
        {
            if (ReferenceEquals(SceneData, node)) return;

            SceneSingleton.Instance.Remove(this);
            SceneData = node;
            SceneSingleton.Instance.Add(this);
        }
    }
}