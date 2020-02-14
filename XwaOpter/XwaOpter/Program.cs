using JeremyAnsel.Xwa.Opt;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace XwaOpter
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            try
            {
                Console.WriteLine("Opt Fix 2.2");

                string openFileName = GetOpenFile();
                if (string.IsNullOrEmpty(openFileName))
                {
                    return;
                }

                var opt = OptFile.FromFile(openFileName);

                string optName = Path.GetFileNameWithoutExtension(opt.FileName);
                string optType;

                if (optName.EndsWith("exterior", StringComparison.OrdinalIgnoreCase))
                {
                    optType = "Exterior";
                    optName = optName.Substring(0, optName.Length - optType.Length);
                }
                else if (optName.EndsWith("cockpit", StringComparison.OrdinalIgnoreCase))
                {
                    optType = "Cockpit";
                    optName = optName.Substring(0, optName.Length - optType.Length);
                }
                else
                {
                    optType = string.Empty;
                }

                Console.WriteLine("{0} [{1}]", optName, optType);
                Console.WriteLine();

                int mmeshIndex = -1;
                string mmeshIndexString = Microsoft.VisualBasic.Interaction.InputBox("Mesh index:\n-1 means whole OPT", "Mesh index", mmeshIndex.ToString(CultureInfo.InvariantCulture));
                if (!string.IsNullOrEmpty(mmeshIndexString))
                {
                    mmeshIndex = int.Parse(mmeshIndexString, CultureInfo.InvariantCulture);
                }

                Console.WriteLine("Mesh index = " + (mmeshIndex < 0 ? "whole OPT" : mmeshIndex.ToString(CultureInfo.InvariantCulture)));
                Console.WriteLine();

                Console.WriteLine("Checking...");
                int facesCount = XwOptChecking(opt, mmeshIndex);
                Console.WriteLine("Checked {0} faces", facesCount);
                Console.WriteLine();

                double threshold = 51.428571428571431;
                string thresholdString = Microsoft.VisualBasic.Interaction.InputBox("Normals threshold in degrees:", "Normals threshold", threshold.ToString(CultureInfo.InvariantCulture));
                if (!string.IsNullOrEmpty(thresholdString))
                {
                    threshold = double.Parse(thresholdString, CultureInfo.InvariantCulture);
                }

                Console.WriteLine("Normals threshold = " + threshold.ToString(CultureInfo.InvariantCulture) + "°");
                Console.WriteLine();

                Console.WriteLine("Computing...");
                XwOptComputing(opt, mmeshIndex, threshold);
                Console.WriteLine("Computed");
                Console.WriteLine();

                string saveFileName = Path.Combine(Path.GetDirectoryName(openFileName), optName + "9" + optType + ".opt");
                saveFileName = GetSaveAsFile(saveFileName);
                if (string.IsNullOrEmpty(saveFileName))
                {
                    return;
                }

                opt.Save(saveFileName);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        static string GetOpenFile()
        {
            var dialog = new OpenFileDialog();
            dialog.DefaultExt = ".opt";
            dialog.CheckFileExists = true;
            dialog.Filter = "OPT files (*.opt)|*.opt";

            if (dialog.ShowDialog() == true)
            {
                return dialog.FileName;
            }

            return null;
        }

        static string GetSaveAsFile(string fileName)
        {
            fileName = Path.GetFullPath(fileName);
            var dialog = new SaveFileDialog();
            dialog.AddExtension = true;
            dialog.DefaultExt = ".opt";
            dialog.Filter = "OPT files (*.opt)|*.opt";
            dialog.InitialDirectory = Path.GetDirectoryName(fileName);
            dialog.FileName = Path.GetFileName(fileName);

            if (dialog.ShowDialog() == true)
            {
                return dialog.FileName;
            }

            return null;
        }

        static int XwOptChecking(OptFile opt, int selectedMeshIndex)
        {
            int meshStart;
            int meshEnd;

            if (selectedMeshIndex < 0)
            {
                meshStart = 0;
                meshEnd = opt.Meshes.Count;
            }
            else
            {
                meshStart = selectedMeshIndex;
                meshEnd = meshStart + 1;

                if (meshStart >= opt.Meshes.Count)
                {
                    return 0;
                }
            }

            int facesCount = 0;

            for (int meshIndex = meshStart; meshIndex < meshEnd; meshIndex++)
            {
                var mesh = opt.Meshes[meshIndex];

                for (int lodIndex = 0; lodIndex < mesh.Lods.Count; lodIndex++)
                {
                    var lod = mesh.Lods[lodIndex];

                    for (int faceGroupIndex = 0; faceGroupIndex < lod.FaceGroups.Count; faceGroupIndex++)
                    {
                        var faceGroup = lod.FaceGroups[faceGroupIndex];

                        for (int faceIndex = 0; faceIndex < faceGroup.Faces.Count; faceIndex++)
                        {
                            var face = faceGroup.Faces[faceIndex];
                            facesCount++;

                            XwVector normal;
                            double angle;
                            double angleSum;
                            XwParseOpt(opt, meshIndex, face, out normal, out angle, out angleSum);

                            var sb = new StringBuilder();
                            sb.AppendFormat(CultureInfo.InvariantCulture, "M:{0} L:{1} B:{2} F:{3} [ ", meshIndex, lodIndex, faceGroupIndex, faceIndex);

                            for (int i = 0; i < face.VerticesCount; i++)
                            {
                                sb.AppendFormat(CultureInfo.InvariantCulture, "{0} ", face.VerticesIndex.AtIndex(i));
                            }

                            sb.Append("]");

                            if (Math.Abs(angleSum - 360.0) >= 2.0)
                            {
                                sb.AppendFormat(CultureInfo.InvariantCulture, " invalid {0}V face", face.VerticesCount);
                                Console.WriteLine(sb);

                                if (face.VerticesCount >= 4)
                                {
                                    double ebp50 = XwVector.SubstractAndLength(
                                        new XwVector(mesh.Vertices[face.VerticesIndex.AtIndex(0)]),
                                        new XwVector(mesh.Vertices[face.VerticesIndex.AtIndex(2)]));

                                    double ebp60 = XwVector.SubstractAndLength(
                                        new XwVector(mesh.Vertices[face.VerticesIndex.AtIndex(1)]),
                                        new XwVector(mesh.Vertices[face.VerticesIndex.AtIndex(3)]));

                                    int[] ebp134 = new int[3];
                                    int[] ebp1AC = new int[3];

                                    if (ebp60 >= ebp50)
                                    {
                                        for (int i = 0; i < 3; i++)
                                        {
                                            ebp134[i] = face.VerticesIndex.AtIndex(i);
                                            ebp1AC[i] = face.VerticesIndex.AtIndex(XwOptGetVertexIndex(i + 2, 4));
                                        }
                                    }
                                    else
                                    {
                                        for (int i = 0; i < 3; i++)
                                        {
                                            if (i < 2)
                                            {
                                                ebp134[i] = face.VerticesIndex.AtIndex(i);
                                            }
                                            else
                                            {
                                                ebp134[i] = face.VerticesIndex.AtIndex(3);
                                            }

                                            ebp1AC[i] = face.VerticesIndex.AtIndex(i + 1);
                                        }
                                    }
                                }
                            }
                            else if (Math.Abs(angle) > 80.0)
                            {
                                sb.AppendFormat(CultureInfo.InvariantCulture, " CW/CCW problem ? ({0:F1}°)", angle);
                                Console.WriteLine(sb);
                            }
                            else if (Math.Abs(angle) >= 2.0)
                            {
                                sb.AppendFormat(CultureInfo.InvariantCulture, " normal recomputed ! ({0:F1}°)", angle);
                                Console.WriteLine(sb);
                            }
                        }
                    }
                }
            }

            return facesCount;
        }

        static void XwOptComputing(OptFile opt, int selectedMeshIndex, double threshold)
        {
            int meshStart;
            int meshEnd;

            if (selectedMeshIndex < 0)
            {
                meshStart = 0;
                meshEnd = opt.Meshes.Count;
            }
            else
            {
                meshStart = selectedMeshIndex;
                meshEnd = meshStart + 1;

                if (meshStart >= opt.Meshes.Count)
                {
                    return;
                }
            }

            var ebp94 = new List<XwVector>();
            var ebp98_vertexIndex = new List<int>();
            var ebp9C_edgesIndex = new List<Tuple<int, int>>();

            var ebpC8 = new List<List<Tuple<Face, Index, int>>>(); // face, vertexIndex, meshIndex

            int maxLodsCount = opt.Meshes
                .Skip(meshStart)
                .Take(meshEnd - meshStart)
                .Max(t => t.Lods.Count);

            for (int i = 0; i < maxLodsCount; i++)
            {
                ebpC8.Add(new List<Tuple<Face, Index, int>>());
            }

            for (int meshIndex = meshStart; meshIndex < meshEnd; meshIndex++)
            {
                var mesh = opt.Meshes[meshIndex];

                ebp94.Clear();
                ebp98_vertexIndex.Clear();

                for (int i = 0; i < mesh.Vertices.Count; i++)
                {
                    ebp98_vertexIndex.Add(-1);
                }

                for (int lodIndex = 0; lodIndex < mesh.Lods.Count; lodIndex++)
                {
                    var lod = mesh.Lods[lodIndex];

                    for (int faceGroupIndex = 0; faceGroupIndex < lod.FaceGroups.Count; faceGroupIndex++)
                    {
                        ebp9C_edgesIndex.Clear();

                        var faceGroup = lod.FaceGroups[faceGroupIndex];

                        ebp9C_edgesIndex.Clear();

                        for (int faceIndex = 0; faceIndex < faceGroup.Faces.Count; faceIndex++)
                        {
                            var face = faceGroup.Faces[faceIndex];

                            var faceTuple = Tuple.Create(face, Index.Empty, meshIndex);
                            ebpC8[lodIndex].Add(faceTuple);

                            XwVector normal;
                            double angle;
                            double angleSum;
                            XwParseOpt(opt, meshIndex, face, out normal, out angle, out angleSum);

                            // TODO
                            face.Normal = normal.ToOptVector();

                            Index item2 = Index.Empty;
                            Index vertexNormalsIndex = Index.Empty;

                            for (int vertexIndex = 0; vertexIndex < faceTuple.Item1.VerticesCount; vertexIndex++)
                            {
                                int ebp18 = faceTuple.Item1.VerticesIndex.AtIndex(vertexIndex);

                                if (ebp98_vertexIndex[ebp18] == -1)
                                {
                                    ebp98_vertexIndex[ebp18] = XwVectorsFindOrAdd(mesh.Vertices[ebp18], ebp94);
                                }

                                item2 = item2.SetAtIndex(vertexIndex, ebp98_vertexIndex[ebp18]);
                                vertexNormalsIndex = vertexNormalsIndex.SetAtIndex(vertexIndex, -1);
                            }

                            faceTuple = Tuple.Create(faceTuple.Item1, item2, faceTuple.Item3);
                            ebpC8[lodIndex][ebpC8[lodIndex].Count - 1] = faceTuple;

                            faceTuple.Item1.VertexNormalsIndex = vertexNormalsIndex;

                            Index edgesIndex = Index.Empty;

                            for (int vertexIndex = 0; vertexIndex < faceTuple.Item1.VerticesCount; vertexIndex++)
                            {
                                int ebp18;

                                if (faceTuple.Item1.VerticesCount - 1 == vertexIndex)
                                {
                                    ebp18 = 0;
                                }
                                else
                                {
                                    ebp18 = vertexIndex + 1;
                                }

                                Tuple<int, int> ebpDC = Tuple.Create(
                                    faceTuple.Item1.VerticesIndex.AtIndex(vertexIndex),
                                    faceTuple.Item1.VerticesIndex.AtIndex(ebp18));

                                edgesIndex = edgesIndex.SetAtIndex(vertexIndex, XwSetsFindOrAdd(ebpDC, ebp9C_edgesIndex));
                            }

                            faceTuple.Item1.EdgesIndex = edgesIndex;
                        }
                    }
                }

                mesh.VertexNormals.Clear();
            }

            for (int lodIndex = 0; lodIndex < maxLodsCount; lodIndex++)
            {
                for (int faceIndex = 0; faceIndex < ebpC8[lodIndex].Count; faceIndex++)
                {
                    var faceTuple = ebpC8[lodIndex][faceIndex];

                    Index vertexNormalsIndex = Index.Empty;

                    for (int vertexIndex = 0; vertexIndex < faceTuple.Item1.VerticesCount; vertexIndex++)
                    {
                        int index = faceTuple.Item2.AtIndex(vertexIndex);

                        XwVector normalSum = new XwVector(faceTuple.Item1.Normal);

                        for (int faceIndex2 = 0; faceIndex2 < ebpC8[lodIndex].Count; faceIndex2++)
                        {
                            if (faceIndex2 == faceIndex)
                            {
                                continue;
                            }

                            var faceTuple2 = ebpC8[lodIndex][faceIndex2];

                            for (int vertexIndex2 = 0; vertexIndex2 < faceTuple2.Item1.VerticesCount; vertexIndex2++)
                            {
                                if (faceTuple2.Item2.AtIndex(vertexIndex2) == index)
                                {
                                    double angle = XwVector.Angle(new XwVector(faceTuple.Item1.Normal), new XwVector(faceTuple2.Item1.Normal));

                                    if (XwVector.AngleRadianToDegree(angle) <= threshold)
                                    {
                                        normalSum = XwVector.Add(normalSum, new XwVector(faceTuple2.Item1.Normal));
                                    }
                                }
                            }
                        }

                        XwVector normal = XwVector.NormalizeAndMultiply(normalSum, 1.0);
                        vertexNormalsIndex = vertexNormalsIndex.SetAtIndex(vertexIndex, XwVectorsFindOrAdd(normal, opt.Meshes[faceTuple.Item3].VertexNormals));
                    }

                    faceTuple.Item1.VertexNormalsIndex = vertexNormalsIndex;
                }
            }
        }

        static int XwOptGetVertexIndex(int index, int count)
        {
            int vertexIndex;

            if (index < 0)
            {
                vertexIndex = count + index;
            }
            else if (index >= count)
            {
                vertexIndex = index - count;
            }
            else
            {
                vertexIndex = index;
            }

            return vertexIndex;
        }


        static void XwParseOpt(OptFile opt, int meshIndex, Face face, out XwVector outNormal, out double outAngle, out double outAngleSum)
        {
            var verticesVertex = new List<XwVector>(face.VerticesCount);
            var verticesLength = new List<double>(face.VerticesCount);
            var verticesAngle = new List<double>(face.VerticesCount);

            for (int index = 0; index < face.VerticesCount; index++)
            {
                int index2 = XwOptGetVertexIndex(index + 1, face.VerticesCount);
                XwVector v1 = new XwVector(opt.Meshes[meshIndex].Vertices[face.VerticesIndex.AtIndex(index)]);
                XwVector v0 = new XwVector(opt.Meshes[meshIndex].Vertices[face.VerticesIndex.AtIndex(index2)]);

                XwVector v = XwVector.Substract(v1, v0);

                verticesVertex.Add(v);
                verticesLength.Add(v.Length());
            }

            outAngleSum = 0.0;

            for (int index = 0; index < face.VerticesCount; index++)
            {
                int index2 = XwOptGetVertexIndex(index - 1, face.VerticesCount);
                double angle = XwVector.AngleRadianToDegree(XwVector.Angle(verticesVertex[index2], verticesVertex[index]));

                verticesAngle.Add(angle);
                outAngleSum += angle;
            }

            int maxLengthVertexIndex = 0;

            for (int index = 1; index < face.VerticesCount; index++)
            {
                if (verticesLength[maxLengthVertexIndex] < verticesLength[index])
                {
                    maxLengthVertexIndex = index;
                }
            }

            if (verticesLength[XwOptGetVertexIndex(maxLengthVertexIndex - 1, face.VerticesCount)] < verticesLength[XwOptGetVertexIndex(maxLengthVertexIndex + 1, face.VerticesCount)])
            {
                maxLengthVertexIndex = XwOptGetVertexIndex(maxLengthVertexIndex + 1, face.VerticesCount);
            }

            XwVector crossV0 = XwVector.Multiply(verticesVertex[XwOptGetVertexIndex(maxLengthVertexIndex - 1, face.VerticesCount)], -1.0);
            XwVector crossV1 = verticesVertex[maxLengthVertexIndex];
            XwVector normal = XwVector.NormalizeAndMultiply(XwVector.CrossProduct(crossV0, crossV1), 1.0);

            XwVector ebpC8 = new XwVector(0.0, 0.0, 0.0);
            XwVector ebpD4 = new XwVector(0.0, 0.0, 0.0);
            XwVector ebpE0 = new XwVector(0.0, 0.0, 0.0);
            XwVector ebpEC = new XwVector(0.0, 0.0, 0.0);

            if (face.VerticesCount == 4)
            {
                maxLengthVertexIndex = XwOptGetVertexIndex(maxLengthVertexIndex + 2, face.VerticesCount);
                int index2 = XwOptGetVertexIndex(maxLengthVertexIndex - 1, face.VerticesCount);

                ebpC8 = XwVector.Multiply(verticesVertex[index2], -1.0);
                ebpD4 = verticesVertex[maxLengthVertexIndex];
                ebpE0 = XwVector.NormalizeAndMultiply(XwVector.CrossProduct(ebpC8, ebpD4), 1.0);
                ebpEC = XwVector.NormalizeAndMultiply(XwVector.Add(normal, ebpE0), 1.0);
            }

            outAngle = XwVector.AngleRadianToDegree(XwVector.Angle(normal, new XwVector(face.Normal)));
            outNormal = normal;
        }

        static int XwVectorsFindOrAdd(Vector vector, IList<XwVector> list)
        {
            XwVector v = new XwVector(vector);

            int index = 0;

            for (; index < list.Count; index++)
            {
                if (XwVector.NearEqual(v, list[index]))
                {
                    break;
                }
            }

            if (index >= list.Count)
            {
                list.Add(v);
            }

            return index;
        }

        static int XwVectorsFindOrAdd(XwVector vector, IList<Vector> list)
        {
            Vector v = vector.ToOptVector();

            int index = 0;

            for (; index < list.Count; index++)
            {
                if (XwVector.NearEqual(vector, new XwVector(list[index])))
                {
                    break;
                }
            }

            if (index >= list.Count)
            {
                list.Add(v);
            }

            return index;
        }

        static bool XwAreSetsEqual(Tuple<int, int> v1, Tuple<int, int> v2)
        {
            if (v1.Item1 == v2.Item1 && v1.Item2 == v2.Item2)
            {
                return true;
            }
            else if (v1.Item1 == v2.Item2 && v1.Item2 == v2.Item1)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        static int XwSetsFindOrAdd(Tuple<int, int> value, List<Tuple<int, int>> list)
        {
            int index = 0;

            for (; index < list.Count; index++)
            {
                if (XwAreSetsEqual(value, list[index]))
                {
                    break;
                }
            }

            if (index >= list.Count)
            {
                list.Add(value);
            }

            return index;
        }
    }
}
