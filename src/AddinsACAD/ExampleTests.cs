using System.Threading;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using NUnit.Framework;
using ServiceACAD;
using TestRunnerACAD;

namespace AddinsACAD
{
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class ExampleTests
    {
        [Test]
        public void TestPass() => Assert.Pass("This test should always passes.");

        [Test]
        public void TestFail() => Assert.Fail("This test should always fail.");
        

        [Test]
        public void TestAddLine2()
        {
            //Use a new drawing
            long lineId = 0;

            void Action1(IDocumentService serviceDoc, Transaction tr)
            {
                var line = new Line(new Point3d(0, 0, 0), new Point3d(100, 100, 100));

                var objectId = tr.AppendEntityToModelSpace(serviceDoc.CadDoc.Database, line);

                lineId = objectId.Handle.Value;
            }

            void Action2(IDocumentService serviceDoc, Transaction tr)
            {
                //Check in another transaction if the line was created

                ObjectId objectId;
                using (var db = serviceDoc.CadDb)
                {
                    if (!db.TryGetObjectId(new Handle(lineId), out objectId))
                    {
                        Assert.Fail("Line didn't created");
                    }
                }
            }

            TestUtils.ExecuteInTransactions("",Action1, Action2);
        }
        //测试成功但是看不到
        // [Test]
        // public void TestEditorWrite()
        // {
        //     void Action1(IDocumentService serviceDoc)
        //     {
        //         // Check if the editor is not null
        //         Assert.IsNotNull(serviceDoc.CadDoc.Editor);
        //         // Write a message to the command line
        //         serviceDoc.CadEd.WriteMessage("\nHello from TestEditorWrite!");
        //     }
        //     TestUtils.ExecuteInCad(Action1);
        // }

    }
}


