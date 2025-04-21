using System;
using Autodesk.AutoCAD.DatabaseServices;

namespace ServiceACAD
{
    public interface IDocumentService
    {
        Database CadDb { get; }
        string DrawingFullPath { get; }
        IEditorService ServiceEd { get; }

<<<<<<< HEAD
        void ExecuteInTransactions(string drawingTitle, params Action<ITransactionService>[] testActions);
=======
        void ExecuteInTransactions(string drawingTitle,params Action<ITransactionService>[] testActions);
>>>>>>> 1f1ac48d411f700a4cb10cdc8df145885fa85ebd
        OpResult<ObjectId[]> Isolate(ObjectId objectId, params ObjectId[] additionalObjectIds);
    }
}
