using System;
using System.Collections.Generic;
using System.Diagnostics;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
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

        public void ExecuteInTransactions(ICollection<Action<IDocumentService, Transaction>> testActions)
        {
            // Lock the document and execute the test actions.
            {
                foreach (var testAction in testActions)
                {
                    ExecuteInTransaction(testAction);
                }
            }
        }

        public void ExecuteInTransaction(Action<IDocumentService, Transaction> testAction)
        {
            using (CadDoc.LockDocument())
            using (var db = CadDoc.Database)
            using (var tr = db.TransactionManager.StartTransaction())
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

        public void Execute(Action<IDocumentService> testAction)
        {
            testAction(this);
        }
    }
}
