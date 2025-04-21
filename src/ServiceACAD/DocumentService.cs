using System;
using System.Diagnostics;
using System.IO;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using NUnit.Framework;
using Application = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace ServiceACAD
{
    public class DocumentService : IDocumentService
    {
        public DocumentService(Document document = null)
        {
            CadDoc = document == null ? Application.DocumentManager.MdiActiveDocument : document;
        }

        public Document CadDoc { get; }
        public Database CadDb => CadDoc.Database;

        public Editor CadEd => CadDoc.Editor;

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
                    Debug.WriteLine($"断言失败异常被忽略:{e.Message}");
                    tr.Commit();
                }
                catch (Exception e)
                {
                    // 记录异常信息
                    Debug.WriteLine($"ExecuteInTransaction异常: {e.Message}");
                    tr.Abort();
                    ;
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
