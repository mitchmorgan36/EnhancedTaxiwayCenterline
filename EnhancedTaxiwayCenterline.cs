using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

[assembly: CommandClass(typeof(EnhancedTaxiwayCenterline.EnhancedTaxiwayCenterlineCommands))]

namespace EnhancedTaxiwayCenterline
{
    public class EnhancedTaxiwayCenterlineCommands
    {
        private const string CommandName = "ENHANCEDCL";
        private const string StorageRootKey = "CMT_ENHANCED_TCL";
        private const double EnhancementLength = 150.0;
        private const double DashLength = 9.0;
        private const double GapLength = 3.0;
        private const double OffsetDistance = 1.25;
        private const double DashWidth = 0.5;
        private const double Epsilon = 1e-6;

        [CommandMethod(CommandName)]
        public void CreateEnhancedTaxiwayCenterline()
        {
            Document acadDoc = Application.DocumentManager.MdiActiveDocument;
            if (acadDoc == null)
            {
                return;
            }

            Editor ed = acadDoc.Editor;
            Database db = acadDoc.Database;

            PromptEntityOptions options = new PromptEntityOptions(
                "\nSelect taxiway centerline polyline. Dashes will start at the polyline start vertex: ");
            options.SetRejectMessage("\nSelect an open lightweight polyline.");
            options.AddAllowedClass(typeof(Polyline), false);

            PromptEntityResult selection = ed.GetEntity(options);
            if (selection.Status != PromptStatus.OK)
            {
                return;
            }

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    Polyline source = tr.GetObject(selection.ObjectId, OpenMode.ForRead) as Polyline;
                    if (source == null)
                    {
                        ed.WriteMessage("\nThe selected object is not a lightweight polyline.");
                        return;
                    }

                    if (source.Closed)
                    {
                        ed.WriteMessage("\nSelect an open polyline.");
                        return;
                    }

                    if (source.Length <= Epsilon)
                    {
                        ed.WriteMessage("\nThe selected polyline has no measurable length.");
                        return;
                    }

                    LayerTableRecord sourceLayer = tr.GetObject(source.LayerId, OpenMode.ForRead) as LayerTableRecord;
                    if (sourceLayer != null && sourceLayer.IsLocked)
                    {
                        ed.WriteMessage("\nThe selected polyline is on a locked layer. Unlock it and rerun the command.");
                        return;
                    }

                    double actualLength = Math.Min(EnhancementLength, source.Length);
                    int removedCount = DeleteExistingManagedDashes(db, source.Handle, tr);

                    BlockTableRecord owner = tr.GetObject(source.OwnerId, OpenMode.ForWrite) as BlockTableRecord;
                    if (owner == null)
                    {
                        ed.WriteMessage("\nCould not access the owner space for the selected polyline.");
                        return;
                    }

                    List<ObjectId> createdIds = new List<ObjectId>();
                    List<DistanceRange> dashRanges = BuildDashRanges(actualLength);

                    foreach (DistanceRange range in dashRanges)
                    {
                        using (Polyline baseDash = CreateDashPolyline(source, range.Start, range.End))
                        {
                            if (baseDash == null)
                            {
                                continue;
                            }

                            if (baseDash.NumberOfVertices < 2)
                            {
                                continue;
                            }

                            foreach (double signedOffset in new[] { OffsetDistance, -OffsetDistance })
                            {
                                Polyline dash = CreateOffsetPolyline(baseDash, signedOffset);
                                if (dash.NumberOfVertices < 2)
                                {
                                    dash.Dispose();
                                    continue;
                                }

                                dash.SetDatabaseDefaults();
                                CopyDisplayProperties(source, dash);
                                SetConstantWidth(dash, DashWidth);

                                owner.AppendEntity(dash);
                                tr.AddNewlyCreatedDBObject(dash, true);
                                createdIds.Add(dash.ObjectId);
                            }
                        }
                    }

                    StoreManagedDashIds(db, source.Handle, createdIds, tr);
                    tr.Commit();

                    string shortenedNote = source.Length + Epsilon < EnhancementLength
                        ? $" The selected polyline is only {source.Length:0.##}' long, so the enhancement was limited to that length."
                        : string.Empty;

                    ed.WriteMessage(
                        $"\nCreated {createdIds.Count} dash polylines for the first {actualLength:0.##}' from the polyline start vertex. " +
                        $"Removed {removedCount} prior script-generated dash polylines.{shortenedNote}");
                }
                catch (System.Exception ex)
                {
                    ed.WriteMessage($"\nError: {ex.Message}");
                }
            }
        }

        private static List<DistanceRange> BuildDashRanges(double totalLength)
        {
            List<DistanceRange> ranges = new List<DistanceRange>();

            for (double dashStart = 0.0; dashStart < totalLength - Epsilon; dashStart += DashLength + GapLength)
            {
                double dashEnd = Math.Min(dashStart + DashLength, totalLength);
                if (dashEnd - dashStart > Epsilon)
                {
                    ranges.Add(new DistanceRange(dashStart, dashEnd));
                }
            }

            return ranges;
        }

        private static Polyline CreateOffsetPolyline(Polyline source, double offsetDistance)
        {
            DBObjectCollection offsets = source.GetOffsetCurves(offsetDistance);
            Polyline result = null;

            foreach (DBObject obj in offsets)
            {
                if (result == null && obj is Polyline polyline)
                {
                    result = polyline;
                }
                else
                {
                    obj.Dispose();
                }
            }

            if (result == null)
            {
                throw new InvalidOperationException("Could not create the required offset curves from the selected polyline.");
            }

            return result;
        }

        private static Polyline CreateDashPolyline(Polyline source, double startDistance, double endDistance)
        {
            double clampedStart = Math.Max(0.0, startDistance);
            double clampedEnd = Math.Min(source.Length, endDistance);

            if (clampedEnd - clampedStart <= Epsilon)
            {
                return null;
            }

            if (clampedStart <= Epsilon && clampedEnd >= source.Length - Epsilon)
            {
                return (Polyline)source.Clone();
            }

            bool hasStartSplit = clampedStart > Epsilon;
            bool hasEndSplit = clampedEnd < source.Length - Epsilon;
            DoubleCollection splitParameters = new DoubleCollection();

            if (hasStartSplit)
            {
                splitParameters.Add(GetParameterAtDistance(source, clampedStart));
            }

            if (hasEndSplit)
            {
                splitParameters.Add(GetParameterAtDistance(source, clampedEnd));
            }

            DBObjectCollection pieces = source.GetSplitCurves(splitParameters);
            int desiredIndex = hasStartSplit && hasEndSplit ? 1 : hasStartSplit ? pieces.Count - 1 : 0;
            Polyline result = null;

            for (int i = 0; i < pieces.Count; i++)
            {
                DBObject piece = pieces[i];
                if (i == desiredIndex && piece is Polyline polyline)
                {
                    result = polyline;
                }
                else
                {
                    piece.Dispose();
                }
            }

            if (result == null)
            {
                throw new InvalidOperationException("Could not extract one of the dash segments from the offset curve.");
            }

            return result;
        }

        private static double GetParameterAtDistance(Polyline source, double distance)
        {
            if (distance <= Epsilon)
            {
                return source.StartParam;
            }

            if (distance >= source.Length - Epsilon)
            {
                return source.EndParam;
            }

            Point3d point = source.GetPointAtDist(distance);
            return source.GetParameterAtPoint(point);
        }

        private static void CopyDisplayProperties(Polyline source, Polyline target)
        {
            target.LayerId = source.LayerId;
            target.Color = source.Color;
            target.LinetypeId = source.LinetypeId;
            target.LinetypeScale = source.LinetypeScale;
            target.LineWeight = source.LineWeight;
            target.Transparency = source.Transparency;
        }

        private static void SetConstantWidth(Polyline polyline, double width)
        {
            for (int i = 0; i < polyline.NumberOfVertices - 1; i++)
            {
                polyline.SetStartWidthAt(i, width);
                polyline.SetEndWidthAt(i, width);
            }
        }

        private static int DeleteExistingManagedDashes(Database db, Handle parentHandle, Transaction tr)
        {
            DBDictionary storage = GetStorageDictionary(db, tr, false);
            if (storage == null)
            {
                return 0;
            }

            string recordKey = GetStorageKey(parentHandle);
            if (!storage.Contains(recordKey))
            {
                return 0;
            }

            Xrecord record = tr.GetObject(storage.GetAt(recordKey), OpenMode.ForRead) as Xrecord;
            if (record?.Data == null)
            {
                return 0;
            }

            int removedCount = 0;

            foreach (TypedValue value in record.Data)
            {
                if (value.TypeCode != (int)DxfCode.SoftPointerId || !(value.Value is ObjectId dashId))
                {
                    continue;
                }

                if (dashId.IsNull || !dashId.IsValid || dashId.IsErased)
                {
                    continue;
                }

                try
                {
                    Entity entity = tr.GetObject(dashId, OpenMode.ForWrite, false) as Entity;
                    if (entity != null && !entity.IsErased)
                    {
                        entity.Erase();
                        removedCount++;
                    }
                }
                catch
                {
                }
            }

            return removedCount;
        }

        private static void StoreManagedDashIds(
            Database db,
            Handle parentHandle,
            IReadOnlyList<ObjectId> dashIds,
            Transaction tr)
        {
            DBDictionary storage = GetStorageDictionary(db, tr, true);
            string recordKey = GetStorageKey(parentHandle);
            ResultBuffer buffer = BuildManagedDashBuffer(dashIds);

            if (storage.Contains(recordKey))
            {
                Xrecord record = tr.GetObject(storage.GetAt(recordKey), OpenMode.ForWrite) as Xrecord;
                if (record != null)
                {
                    record.Data = buffer;
                }

                return;
            }

            Xrecord newRecord = new Xrecord
            {
                Data = buffer
            };

            storage.SetAt(recordKey, newRecord);
            tr.AddNewlyCreatedDBObject(newRecord, true);
        }

        private static DBDictionary GetStorageDictionary(Database db, Transaction tr, bool createIfMissing)
        {
            DBDictionary namedObjects = tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForRead) as DBDictionary;
            if (namedObjects == null)
            {
                return null;
            }

            if (namedObjects.Contains(StorageRootKey))
            {
                OpenMode mode = createIfMissing ? OpenMode.ForWrite : OpenMode.ForRead;
                return tr.GetObject(namedObjects.GetAt(StorageRootKey), mode) as DBDictionary;
            }

            if (!createIfMissing)
            {
                return null;
            }

            namedObjects.UpgradeOpen();

            DBDictionary storage = new DBDictionary();
            namedObjects.SetAt(StorageRootKey, storage);
            tr.AddNewlyCreatedDBObject(storage, true);
            return storage;
        }

        private static string GetStorageKey(Handle parentHandle)
        {
            return $"H_{parentHandle}";
        }

        private static ResultBuffer BuildManagedDashBuffer(IReadOnlyList<ObjectId> dashIds)
        {
            List<TypedValue> values = new List<TypedValue>
            {
                new TypedValue((int)DxfCode.Text, CommandName)
            };

            foreach (ObjectId dashId in dashIds)
            {
                values.Add(new TypedValue((int)DxfCode.SoftPointerId, dashId));
            }

            return new ResultBuffer(values.ToArray());
        }

        private readonly struct DistanceRange
        {
            public DistanceRange(double start, double end)
            {
                Start = start;
                End = end;
            }

            public double Start { get; }

            public double End { get; }
        }
    }
}
