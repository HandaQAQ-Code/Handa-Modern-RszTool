using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text.Json;

namespace RszTool.App.ViewModels
{
    public sealed class TransformPointJson
    {
        public int Index { get; set; }
        public Vector3 Position { get; set; }
        public Quaternion Rotation { get; set; }

        public string Display => string.Format(
            CultureInfo.InvariantCulture,
            "{0:000}: T({1:0.######}, {2:0.######}, {3:0.######}, 0)    R({4:0.######}, {5:0.######}, {6:0.######}, {7:0.######})",
            Index,
            Position.X,
            Position.Y,
            Position.Z,
            Rotation.X,
            Rotation.Y,
            Rotation.Z,
            Rotation.W);
    }

    public sealed class TransformPointJsonGroup
    {
        public string Name { get; }
        public List<TransformPointJson> Points { get; }

        public TransformPointJsonGroup(string name, List<TransformPointJson> points)
        {
            Name = name;
            Points = points;
        }

        public override string ToString() => $"{Name} ({Points.Count})";
    }

    public sealed class TransformPointsJsonFile
    {
        public List<TransformPointJsonGroup> Groups { get; }
        public string? ActiveGroup { get; }

        public TransformPointsJsonFile(List<TransformPointJsonGroup> groups, string? activeGroup)
        {
            Groups = groups;
            ActiveGroup = activeGroup;
        }
    }

    public static class TransformPointsJsonReader
    {
        private static readonly string[] TransformNames = ["transform", "position", "pos", "translation", "v0"];
        private static readonly string[] RotationNames = ["rotation", "rot", "orientation", "quaternion", "v1"];

        public static TransformPointsJsonFile Load(string path)
        {
            using FileStream stream = File.OpenRead(path);
            using JsonDocument document = JsonDocument.Parse(stream, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
            });

            JsonElement root = document.RootElement;
            string? activeGroup = null;
            List<TransformPointJsonGroup> groups;
            if (root.ValueKind == JsonValueKind.Object)
            {
                activeGroup = GetStringProperty(root, "activeGroup") ??
                    GetStringProperty(root, "active_group") ??
                    GetStringProperty(root, "currentGroup");

                if (TryGetPropertyIgnoreCase(root, "groups", out JsonElement groupsElement))
                {
                    groups = ReadGroups(groupsElement);
                }
                else if (TryGetPropertyIgnoreCase(root, "points", out JsonElement pointsElement))
                {
                    groups = [ReadGroup(Path.GetFileNameWithoutExtension(path), pointsElement)];
                }
                else
                {
                    throw new InvalidDataException("JSON must contain a groups array or a points array.");
                }
            }
            else if (root.ValueKind == JsonValueKind.Array)
            {
                groups = ReadRootArray(root, Path.GetFileNameWithoutExtension(path));
            }
            else
            {
                throw new InvalidDataException("JSON root must be an object or an array.");
            }

            if (groups.Count == 0)
            {
                throw new InvalidDataException("JSON does not contain any point groups.");
            }
            return new TransformPointsJsonFile(groups, activeGroup);
        }

        private static List<TransformPointJsonGroup> ReadRootArray(JsonElement root, string defaultName)
        {
            List<JsonElement> items = root.EnumerateArray().ToList();
            if (items.Count > 0 && items.All(item =>
                item.ValueKind == JsonValueKind.Object &&
                TryGetPropertyIgnoreCase(item, "points", out _)))
            {
                return ReadGroups(root);
            }
            return [ReadGroup(defaultName, root)];
        }

        private static List<TransformPointJsonGroup> ReadGroups(JsonElement groupsElement)
        {
            if (groupsElement.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException("groups must be an array.");
            }

            List<TransformPointJsonGroup> groups = [];
            int index = 1;
            foreach (JsonElement groupElement in groupsElement.EnumerateArray())
            {
                if (groupElement.ValueKind != JsonValueKind.Object ||
                    !TryGetPropertyIgnoreCase(groupElement, "points", out JsonElement pointsElement))
                {
                    throw new InvalidDataException($"Group {index} must contain a points array.");
                }

                string name = GetStringProperty(groupElement, "name") ?? $"Group {index}";
                groups.Add(ReadGroup(name, pointsElement));
                index++;
            }
            return groups;
        }

        private static TransformPointJsonGroup ReadGroup(string name, JsonElement pointsElement)
        {
            if (pointsElement.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException($"Group '{name}' points must be an array.");
            }

            List<TransformPointJson> points = [];
            int index = 1;
            foreach (JsonElement pointElement in pointsElement.EnumerateArray())
            {
                points.Add(ReadPoint(pointElement, index));
                index++;
            }
            return new TransformPointJsonGroup(name, points);
        }

        private static TransformPointJson ReadPoint(JsonElement element, int index)
        {
            if (element.ValueKind == JsonValueKind.Array)
            {
                List<JsonElement> values = element.EnumerateArray().ToList();
                if (values.Count == 2 &&
                    TryReadVector3(values[0], out Vector3 pairPosition) &&
                    TryReadQuaternion(values[1], out Quaternion pairRotation))
                {
                    return CreatePoint(index, pairPosition, pairRotation);
                }
                if (values.Count >= 7 &&
                    TryReadFloat(values[0], out float x) &&
                    TryReadFloat(values[1], out float y) &&
                    TryReadFloat(values[2], out float z) &&
                    TryReadFloat(values[3], out float a) &&
                    TryReadFloat(values[4], out float b) &&
                    TryReadFloat(values[5], out float c) &&
                    TryReadFloat(values[6], out float d))
                {
                    return CreatePoint(index, new Vector3(x, y, z), new Quaternion(a, b, c, d));
                }
                if (values.Count >= 3 &&
                    TryReadFloat(values[0], out float positionX) &&
                    TryReadFloat(values[1], out float positionY) &&
                    TryReadFloat(values[2], out float positionZ))
                {
                    return CreatePoint(index, new Vector3(positionX, positionY, positionZ), Quaternion.Identity);
                }
                throw new InvalidDataException($"Point {index} must contain position and rotation values.");
            }

            if (element.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException($"Point {index} must be an object or an array.");
            }

            Vector3 position;
            if (!TryReadNamedVector3(element, TransformNames, out position) &&
                !TryReadVector3(element, out position))
            {
                throw new InvalidDataException($"Point {index} has no numeric transform/position.");
            }

            if (!TryReadNamedQuaternion(element, RotationNames, out Quaternion rotation) &&
                !TryReadQuaternion(element, out rotation))
            {
                return CreatePoint(index, position, Quaternion.Identity);
            }
            return CreatePoint(index, position, rotation);
        }

        private static TransformPointJson CreatePoint(int index, Vector3 position, Quaternion rotation)
        {
            return new TransformPointJson
            {
                Index = index,
                Position = position,
                Rotation = rotation,
            };
        }

        private static bool TryReadNamedVector3(JsonElement element, IEnumerable<string> names, out Vector3 value)
        {
            foreach (string name in names)
            {
                if (TryGetPropertyIgnoreCase(element, name, out JsonElement property) &&
                    TryReadVector3(property, out value))
                {
                    return true;
                }
            }
            value = default;
            return false;
        }

        private static bool TryReadNamedQuaternion(JsonElement element, IEnumerable<string> names, out Quaternion value)
        {
            foreach (string name in names)
            {
                if (TryGetPropertyIgnoreCase(element, name, out JsonElement property) &&
                    TryReadQuaternion(property, out value))
                {
                    return true;
                }
            }
            value = default;
            return false;
        }

        private static bool TryReadVector3(JsonElement element, out Vector3 value)
        {
            value = default;
            if (element.ValueKind == JsonValueKind.Array)
            {
                List<JsonElement> values = element.EnumerateArray().ToList();
                return values.Count >= 3 &&
                    TryReadFloat(values[0], out float x) &&
                    TryReadFloat(values[1], out float y) &&
                    TryReadFloat(values[2], out float z) &&
                    SetVector3(out value, x, y, z);
            }
            if (element.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            return TryReadNamedFloat(element, ["x", "X"], out float objectX) &&
                TryReadNamedFloat(element, ["y", "Y"], out float objectY) &&
                TryReadNamedFloat(element, ["z", "Z"], out float objectZ) &&
                SetVector3(out value, objectX, objectY, objectZ);
        }

        private static bool TryReadQuaternion(JsonElement element, out Quaternion value)
        {
            value = default;
            if (element.ValueKind == JsonValueKind.Array)
            {
                List<JsonElement> values = element.EnumerateArray().ToList();
                return values.Count >= 4 &&
                    TryReadFloat(values[0], out float x) &&
                    TryReadFloat(values[1], out float y) &&
                    TryReadFloat(values[2], out float z) &&
                    TryReadFloat(values[3], out float w) &&
                    SetQuaternion(out value, x, y, z, w);
            }
            if (element.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            bool hasA = TryReadNamedFloat(element, ["a"], out float a);
            bool hasB = TryReadNamedFloat(element, ["b"], out float b);
            bool hasC = TryReadNamedFloat(element, ["c"], out float c);
            bool hasD = TryReadNamedFloat(element, ["d"], out float d);
            bool hasAbcd = hasA && hasB && hasC && hasD;
            if (hasAbcd)
            {
                return SetQuaternion(out value, a, b, c, d);
            }

            float objectX = 0;
            float objectY = 0;
            float objectZ = 0;
            float objectW = 0;
            bool hasXyzW = TryReadNamedFloat(element, ["x", "X"], out objectX) &&
                TryReadNamedFloat(element, ["y", "Y"], out objectY) &&
                TryReadNamedFloat(element, ["z", "Z"], out objectZ) &&
                TryReadNamedFloat(element, ["w", "W"], out objectW);
            return hasXyzW && SetQuaternion(out value, objectX, objectY, objectZ, objectW);
        }

        private static bool SetVector3(out Vector3 value, float x, float y, float z)
        {
            value = new Vector3(x, y, z);
            return true;
        }

        private static bool SetQuaternion(out Quaternion value, float x, float y, float z, float w)
        {
            value = new Quaternion(x, y, z, w);
            return true;
        }

        private static bool TryReadNamedFloat(JsonElement element, IEnumerable<string> names, out float value)
        {
            foreach (string name in names)
            {
                if (TryGetPropertyIgnoreCase(element, name, out JsonElement property) &&
                    TryReadFloat(property, out value))
                {
                    return true;
                }
            }
            value = 0;
            return false;
        }

        private static bool TryReadFloat(JsonElement element, out float value)
        {
            value = 0;
            return element.ValueKind switch
            {
                JsonValueKind.Number => element.TryGetSingle(out value),
                JsonValueKind.String => float.TryParse(
                    element.GetString(),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out value),
                _ => false,
            };
        }

        private static string? GetStringProperty(JsonElement element, string name)
        {
            return TryGetPropertyIgnoreCase(element, name, out JsonElement value) &&
                value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        }

        private static bool TryGetPropertyIgnoreCase(JsonElement element, string name, out JsonElement value)
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
            value = default;
            return false;
        }
    }
}
