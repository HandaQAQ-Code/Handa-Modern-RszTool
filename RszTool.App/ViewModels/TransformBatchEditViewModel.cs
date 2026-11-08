using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;

namespace RszTool.App.ViewModels
{
    public enum TransformBatchEditMode
    {
        Add,
        Subtract,
        Set,
        Increment,
        Decrement,
    }

    public class TransformBatchAxisEdit
    {
        public string Name { get; set; } = "";
        public bool Enabled { get; set; }
        public TransformBatchEditMode Mode { get; set; } = TransformBatchEditMode.Add;
        public float Value { get; set; }

        public float Apply(float originalValue, int objectIndex)
        {
            return Mode switch
            {
                TransformBatchEditMode.Add => originalValue + Value,
                TransformBatchEditMode.Subtract => originalValue - Value,
                TransformBatchEditMode.Set => Value,
                TransformBatchEditMode.Increment => originalValue + Value * objectIndex,
                TransformBatchEditMode.Decrement => originalValue - Value * objectIndex,
                _ => originalValue,
            };
        }
    }

    public class TransformBatchEditViewModel
    {
        public TransformBatchAxisEdit X { get; } = new() { Name = "X" };
        public TransformBatchAxisEdit Y { get; } = new() { Name = "Y" };
        public TransformBatchAxisEdit Z { get; } = new() { Name = "Z" };
        public TransformBatchAxisEdit J { get; } = new() { Name = "J" };

        public TransformBatchAxisEdit[] Axes => [X, Y, Z, J];

        public int SelectedGameObjectCount { get; set; }
        public int TransformCount { get; private set; }

        public int Apply(IEnumerable<ScnFile.GameObjectData> gameObjects)
        {
            TransformCount = 0;
            int objectIndex = 0;
            foreach (var gameObject in gameObjects)
            {
                bool changed = false;
                HashSet<RszInstance> visited = new();
                foreach (var transform in TransformV0Helper.IterTransforms(gameObject, visited))
                {
                    if (ApplyTransform(transform, objectIndex))
                    {
                        changed = true;
                        TransformCount++;
                    }
                }
                if (changed)
                {
                    objectIndex++;
                }
            }
            return TransformCount;
        }

        private bool ApplyTransform(RszInstance transform, int objectIndex)
        {
            int index = transform.RszClass.IndexOfField("v0");
            if (index < 0 || transform.Values.Length <= index) return false;
            var value = transform.Values[index];
            if (value is Vector4 vector)
            {
                transform.Values[index] = ApplyVector(vector, objectIndex);
                return true;
            }
            if (value is Quaternion quaternion)
            {
                var vectorValue = ApplyVector(new Vector4(quaternion.X, quaternion.Y, quaternion.Z, quaternion.W), objectIndex);
                transform.Values[index] = new Quaternion(vectorValue.X, vectorValue.Y, vectorValue.Z, vectorValue.W);
                return true;
            }
            return false;
        }

        private Vector4 ApplyVector(Vector4 value, int objectIndex)
        {
            if (X.Enabled) value.X = X.Apply(value.X, objectIndex);
            if (Y.Enabled) value.Y = Y.Apply(value.Y, objectIndex);
            if (Z.Enabled) value.Z = Z.Apply(value.Z, objectIndex);
            if (J.Enabled) value.W = J.Apply(value.W, objectIndex);
            return value;
        }
    }

    public static class TransformV0Helper
    {
        private const string TransformClassName = "via.Transform";
        private const string TransformFieldName = "v0";
        private const string NumberPattern = @"[+-]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][+-]?\d+)?";
        private static readonly Regex ClipboardVectorRegex = new(
    @"^\s*(?<x>" + NumberPattern + @")\s*[,\uFF0C]\s*(?<y>" + NumberPattern + @")\s*[,\uFF0C]\s*(?<z>" + NumberPattern + @")(?:\s*[,\uFF0C]\s*(?<w>" + NumberPattern + @"))?\s*$",
    RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static bool TryParseClipboardVector(string text, out Vector4 value)
        {
            value = default;
            Match match = ClipboardVectorRegex.Match(text);
            if (!match.Success) return false;

            // 强制解析 x, y, z
            if (!float.TryParse(match.Groups["x"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float x) ||
                !float.TryParse(match.Groups["y"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float y) ||
                !float.TryParse(match.Groups["z"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
            {
                return false;
            }

            // w 可选，默认 0
            float w = 0f;
            if (match.Groups["w"].Success)
            {
                if (!float.TryParse(match.Groups["w"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out w))
                    return false;
            }

            value = new Vector4(x, y, z, w);
            return true;
        }
        public static int SetTransformV0(IEnumerable<IGameObjectData> gameObjects, Vector4 value)
        {
            int count = 0;
            foreach (var gameObject in gameObjects)
            {
                HashSet<RszInstance> visited = new();
                foreach (var transform in IterTransforms(gameObject, visited))
                {
                    if (SetSingleTransformV0(transform, value))
                    {
                        count++;
                    }
                }
            }
            return count;
        }

        public static int SetTransformV0(RszInstance instance, Vector4 value)
        {
            int count = 0;
            HashSet<RszInstance> visited = new();
            foreach (var transform in IterTransforms(instance, visited))
            {
                if (SetSingleTransformV0(transform, value))
                {
                    count++;
                }
            }
            return count;
        }

        public static IEnumerable<RszInstance> IterTransforms(IGameObjectData gameObject, HashSet<RszInstance> visited)
        {
            if (gameObject.Instance?.RszClass.name == TransformClassName)
            {
                yield return gameObject.Instance;
            }
            foreach (var component in gameObject.Components)
            {
                foreach (var transform in IterTransforms(component, visited))
                {
                    yield return transform;
                }
            }
        }

        public static IEnumerable<RszInstance> IterTransforms(RszInstance instance, HashSet<RszInstance> visited)
        {
            if (!visited.Add(instance)) yield break;
            if (instance.RszClass.name == TransformClassName)
            {
                yield return instance;
            }
            if (instance.RSZUserData != null) yield break;
            foreach (var value in instance.Values)
            {
                if (value is RszInstance child)
                {
                    foreach (var transform in IterTransforms(child, visited))
                    {
                        yield return transform;
                    }
                }
                else if (value is List<object> items)
                {
                    foreach (var item in items)
                    {
                        if (item is not RszInstance childItem) continue;
                        foreach (var transform in IterTransforms(childItem, visited))
                        {
                            yield return transform;
                        }
                    }
                }
            }
        }

        private static bool SetSingleTransformV0(RszInstance transform, Vector4 value)
        {
            int index = transform.RszClass.IndexOfField(TransformFieldName);
            if (index < 0 || transform.Values.Length <= index) return false;
            object currentValue = transform.Values[index];
            if (currentValue is Vector4)
            {
                transform.SetFieldValue(index, value);
                return true;
            }
            if (currentValue is Quaternion)
            {
                transform.SetFieldValue(index, new Quaternion(value.X, value.Y, value.Z, value.W));
                return true;
            }
            return false;
        }
    }
}
