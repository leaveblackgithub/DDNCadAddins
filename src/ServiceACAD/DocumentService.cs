using System;
using System.Collections.Generic;
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

        public Editor CadEd=> CadDoc.Editor;

        public void ExecuteInTransactions(string drawingTitle,ICollection<Action<IDocumentService, Transaction>> testActions)
        {

            if (!TitleEquals(drawingTitle))
            {
                Assert.Ignore($"\nThe test is ignored since active drawing is not required {drawingTitle}");
                return;
            }
            {
                foreach (var testAction in testActions)
                {
                    ExecuteInTransaction( testAction);
                }
            }
        }

        private void ExecuteInTransaction(Action<IDocumentService, Transaction> testAction)
        {
            //怎样检查cad文件是否已存盘

            // Replace 'var lock' with 'var docLock' to avoid conflicting with 'lock' keyword.
            using (CadDoc.LockDocument())
            using (var tr = CadDb.TransactionManager.StartTransaction())
            {
                try
                {
                    // Execute the test action.
                    testAction(this, tr);
                    tr.Commit();
                }
                catch (Exception e)
                {
                    // Replace Trace.Write with Debug.Write
                    Debug.Write(e.Message);
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
                var drawingName = System.IO.Path.GetFileNameWithoutExtension(CadDoc.Name);
                return string.Equals(drawingName, drawingTitle, StringComparison.CurrentCultureIgnoreCase);

            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                return false;
            }
        }

        public void ExecuteInDoc(Action<IDocumentService> testAction, string drawingTitle)
        {

            if (!TitleEquals(drawingTitle))
            {
                Assert.Ignore($"\nThe test is ignored since active drawing is not required {drawingTitle}");
                return;
            }

            try
            {
                testAction(this);

            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
            }
        }

        public string DrawingFullPath=> CadDoc.Name;
        
    }
}
