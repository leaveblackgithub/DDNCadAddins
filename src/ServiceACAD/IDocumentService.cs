using System;
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

        void ExecuteInTransactions(string drawingTitle,params Action<ITransactionService>[] testActions);
    }
}
