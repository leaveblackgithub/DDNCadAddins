using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;

namespace ServiceACAD
{
    public interface IDocumentService
    {
        Database CadDb { get; }
        string DrawingFullPath { get; }
        IEditorService ServiceEd { get; }

        void ExecuteInTransactions(string drawingTitle,params Action<ITransactionService>[] testActions);
        OpResult<ObjectId[]> Isolate(ObjectId objectId, params ObjectId[] additionalObjectIds);
    }
}
