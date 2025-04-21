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
        string DrawingFullPath { get; }
        void ExecuteInTransactions(string drawingTitle,ICollection<Action<IDocumentService, Transaction>> testActions);
        
        void ExecuteInDoc(Action<IDocumentService> testAction,string drawingTitle);

        void ExecuteInTransactions(string drawingTitle,params Action<ITransactionService>[] testActions);
    }
}
