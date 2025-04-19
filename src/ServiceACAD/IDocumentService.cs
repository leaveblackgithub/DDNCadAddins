using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;

namespace ServiceACAD
{
    public interface IDocumentService
    {
        Document CadDoc { get; }
        Database CadDb { get; }
        Editor CadEd { get; }
        void ExecuteInTransactions(ICollection<Action<IDocumentService, Transaction>> testActions);

        void ExecuteInTransaction(Action<IDocumentService, Transaction> testAction);
        void Execute(Action<IDocumentService> testAction);
    }
}
