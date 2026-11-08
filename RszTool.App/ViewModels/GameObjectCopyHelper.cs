using RszTool.App.Views;
using System.Windows;

namespace RszTool.App.ViewModels
{
    public static class GameObjectCopyHelper
    {
        private static PfbFile.GameObjectData? CopiedPfbGameObject { get; set; }
        private static ScnFile.GameObjectData? CopiedScnGameObject { get; set; }
        private static List<PfbFile.GameObjectData>? CopiedPfbGameObjects { get; set; }
        private static List<ScnFile.GameObjectData>? CopiedScnGameObjects { get; set; }

        public static void CopyGameObject(PfbFile.GameObjectData gameObject)
        {
            CopiedPfbGameObject = gameObject;
            CopiedScnGameObject = null;
            CopiedPfbGameObjects = null;
            CopiedScnGameObjects = null;
        }

        public static void CopyGameObject(ScnFile.GameObjectData gameObject)
        {
            CopiedScnGameObject = gameObject;
            CopiedPfbGameObject = null;
            CopiedPfbGameObjects = null;
            CopiedScnGameObjects = null;
        }

        public static void CopyGameObjects(IEnumerable<ScnFile.GameObjectData> gameObjects)
        {
            CopiedScnGameObjects = gameObjects.ToList();
            CopiedPfbGameObjects = null;
            CopiedScnGameObject = CopiedScnGameObjects.Count == 1 ? CopiedScnGameObjects[0] : null;
            CopiedPfbGameObject = null;
        }

        public static void CopyGameObjects(IEnumerable<PfbFile.GameObjectData> gameObjects)
        {
            CopiedPfbGameObjects = gameObjects.ToList();
            CopiedScnGameObjects = null;
            CopiedPfbGameObject = CopiedPfbGameObjects.Count == 1 ? CopiedPfbGameObjects[0] : null;
            CopiedScnGameObject = null;
        }

        public static PfbFile.GameObjectData? GetCopiedPfbGameObject()
        {
            if (CopiedPfbGameObject != null) return CopiedPfbGameObject;
            if (CopiedPfbGameObjects?.Count > 0) return CopiedPfbGameObjects[0];
            if (CopiedScnGameObject != null)
                return PfbFile.GameObjectData.FromScnGameObject(CopiedScnGameObject);
            if (CopiedScnGameObjects?.Count > 0)
                return PfbFile.GameObjectData.FromScnGameObject(CopiedScnGameObjects[0]);
            return null;
        }

        public static ScnFile.GameObjectData? GetCopiedScnGameObject()
        {
            if (CopiedScnGameObject != null) return CopiedScnGameObject;
            if (CopiedScnGameObjects?.Count > 0) return CopiedScnGameObjects[0];
            if (CopiedPfbGameObject != null)
                return ScnFile.GameObjectData.FromPfbGameObject(CopiedPfbGameObject);
            if (CopiedPfbGameObjects?.Count > 0)
                return ScnFile.GameObjectData.FromPfbGameObject(CopiedPfbGameObjects[0]);
            return null;
        }

        public static IEnumerable<ScnFile.GameObjectData> GetCopiedScnGameObjects()
        {
            if (CopiedScnGameObjects?.Count > 0) return CopiedScnGameObjects;
            if (CopiedScnGameObject != null) return [CopiedScnGameObject];
            if (CopiedPfbGameObjects?.Count > 0)
                return CopiedPfbGameObjects.Select(ScnFile.GameObjectData.FromPfbGameObject).ToList();
            if (CopiedPfbGameObject != null) return [ScnFile.GameObjectData.FromPfbGameObject(CopiedPfbGameObject)];
            return [];
        }

        public static IEnumerable<PfbFile.GameObjectData> GetCopiedPfbGameObjects()
        {
            if (CopiedPfbGameObjects?.Count > 0) return CopiedPfbGameObjects;
            if (CopiedPfbGameObject != null) return [CopiedPfbGameObject];
            if (CopiedScnGameObjects?.Count > 0)
                return CopiedScnGameObjects.Select(PfbFile.GameObjectData.FromScnGameObject).ToList();
            if (CopiedScnGameObject != null) return [PfbFile.GameObjectData.FromScnGameObject(CopiedScnGameObject)];
            return [];
        }

        /// <summary>
        /// Update re4 chainsaw.ContextID
        /// </summary>
        /// <param name="fileOption"></param>
        /// <param name="gameObjectData"></param>
        public static void UpdateContextID(RszFileOption fileOption, IGameObjectData gameObjectData)
        {
            if (fileOption.GameName == GameName.re4)
            {
                var contextIDs = IterGameObjectContextID(gameObjectData).ToArray();
                if (contextIDs.Length > 0)
                {
                    UpdateContextIDWindow dialog = new()
                    {
                        TreeViewItems = contextIDs
                    };
                    dialog.ShowDialog();
                }
            }
        }

        public static void UpdateContextIDs(RszFileOption fileOption, IEnumerable<IGameObjectData> gameObjectDatas)
        {
            if (fileOption.GameName == GameName.re4)
            {
                var contextIDs = gameObjectDatas.SelectMany(IterGameObjectContextID).ToArray();
                if (contextIDs.Length > 0)
                {
                    UpdateContextIDWindow dialog = new()
                    {
                        TreeViewItems = contextIDs
                    };
                    dialog.ShowDialog();
                }
            }
        }

        public static void UpdateContextIDBatch(RszFileOption fileOption, IGameObjectData gameObjectData,int Index,int Group)
        {
            if (fileOption.GameName == GameName.re4)
            {
                var contextIDs = IterGameObjectContextID(gameObjectData).ToArray();
                if (contextIDs.Length > 0)
                {
                    contextIDs[0].Items[0].Value = Group;
                    contextIDs[0].Items[1].Value = Index;
                }
                
            }
        }

        /// <summary>
        /// Update re4 chainsaw.ContextID
        /// </summary>
        /// <param name="fileOption"></param>
        /// <param name="gameObjectData"></param>
        public static void UpdateInstanceContextID(RszFileOption fileOption, RszInstance instance)
        {
            if (fileOption.GameName == GameName.re4)
            {
                var contextIDs = IterInstanceContextID(instance).ToArray();
                if (contextIDs.Length > 0)
                {
                    UpdateContextIDWindow dialog = new()
                    {
                        TreeViewItems = contextIDs
                    };
                    dialog.ShowDialog();
                }
            }
        }

        /// <summary>
        /// 迭代GameObject以及子物体的组件，查找re4 chainsaw.ContextID
        /// </summary>
        /// <param name="gameObject"></param>
        /// <returns></returns>
        public static IEnumerable<GameObjectContextID> IterGameObjectContextID(IGameObjectData gameObject)
        {
            foreach (var (path, instance) in Re4IdEditor.Walk(Re4IdEditor.GameObjectRoots(gameObject, true)))
                if (instance.RszClass.name == "chainsaw.ContextID")
                    yield return new GameObjectContextID(path, instance);
        }

        public static IEnumerable<GameObjectContextID> IterInstanceContextID(RszInstance instance)
        {
            foreach (var (path, current) in Re4IdEditor.Walk([(instance.Name, instance)]))
                if (current.RszClass.name == "chainsaw.ContextID")
                    yield return new GameObjectContextID(path, current);
        }

    }


    /// <summary>
    /// For re4 chainsaw.ContextID, when paste game object, update the ContextID
    /// </summary>
    public class GameObjectContextID
    {
        public GameObjectContextID(string name, RszInstance instance)
        {
            Name = name;
            if (instance.RszClass.name != "chainsaw.ContextID")
            {
                throw new ArgumentException("Expected chainsaw.ContextID");
            }
            int groupIndex = instance.RszClass.IndexOfField("_Group");
            int indexIndex = instance.RszClass.IndexOfField("_Index");
            RszFieldNormalViewModel groupViewModel = new(instance, groupIndex);
            RszFieldNormalViewModel indexViewModel = new(instance, indexIndex);
            Items = [
                groupViewModel,
                indexViewModel,
            ];

            groupViewModel.PropertyChanged += (o, e) => {
                App.Instance.SaveData.LastContextID.Group = (int)groupViewModel.Value;
            };
            indexViewModel.PropertyChanged += (o, e) => {
                App.Instance.SaveData.LastContextID.Index = (int)indexViewModel.Value;
            };
        }

        public string Name { get; set; }
        public RszFieldNormalViewModel[] Items { get; set; }
    }
}
