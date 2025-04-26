using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using NUnit.Framework;
using Application = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using Autodesk.AutoCAD.EditorInput;

namespace ServiceACAD
{
    public class DocumentService : IDocumentService
    {
        public DocumentService()
        {
            CadDoc = Application.DocumentManager.MdiActiveDocument;
            ServiceEd = new EditorService(CadDoc.Editor);
        }

        public DocumentService(Document document = null)
        {
            CadDoc = document == null ? Application.DocumentManager.MdiActiveDocument : document;
            ServiceEd = new EditorService(CadDoc.Editor);
        }
        
        public Document CadDoc { get; }
        public Database CadDb => CadDoc.Database;
        public IEditorService ServiceEd { get; }

        public OpResult<ObjectId[]> Isolate(ObjectId objectId, params ObjectId[] additionalObjectIds)
        {
            try
            {
                // 合并所有需要隔离的ObjectId
                var objectsToIsolate = new List<ObjectId> { objectId };
                if (additionalObjectIds != null && additionalObjectIds.Length > 0)
                {
                    objectsToIsolate.AddRange(additionalObjectIds);
                }

                // 过滤无效的ObjectId
                objectsToIsolate = objectsToIsolate.Where(id => id.IsValid).ToList();

                if (objectsToIsolate.Count == 0)
                {
                    return OpResult<ObjectId[]>.Fail("没有有效的对象可隔离");
                }

                // 执行隔离操作
                ExecuteInTransaction(serviceTrans => serviceTrans.IsolateObjectsOfModelSpace(objectsToIsolate));

                return OpResult<ObjectId[]>.Success(objectsToIsolate.ToArray());
            }
            catch (Exception ex)
            {
                Logger._.Error($"隔离对象失败: {ex.Message}");
                return OpResult<ObjectId[]>.Fail($"隔离对象失败: {ex.Message}");
            }
        }


        public void ExecuteInTransactions(string drawingTitle, params Action<ITransactionService>[] testActions)
        {
            if (!TitleEquals(drawingTitle))
            {
                Assert.Ignore($"\nThe test is ignored since active drawing is not required {drawingTitle}");
                return;
            }

            foreach (var testAction in testActions)
            {
                ExecuteInTransaction(testAction);
            }
        }

        public string DrawingFullPath => CadDoc.Name;

        public void ExecuteInTransaction(Action<ITransactionService> testAction)
        {
            using (CadDoc.LockDocument())
            using (var tr = CadDb.TransactionManager.StartTransaction())
            {
                try
                {
                    var transactionService = new TransactionService(tr);
                    testAction(transactionService);
                    tr.Commit();
                }
                catch (AssertionException e)
                {
                    // 断言失败异常被忽略，仅记录不抛出
                    Logger._.Warn($"断言失败异常被忽略: {e.Message}");
                    tr.Commit();
                }
                catch (Exception e)
                {
                    Logger._.Error("处理文档时发生错误", e);
                    tr.Abort();
                }
            }
        }

        private bool TitleEquals(string drawingTitle)
        {
            if (string.IsNullOrEmpty(drawingTitle))
            {
                return true;
            }

            try
            {
                var drawingName = Path.GetFileNameWithoutExtension(CadDoc.Name);
                return string.Equals(drawingName, drawingTitle, StringComparison.CurrentCultureIgnoreCase);
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                return false;
            }
        }
    }
}
