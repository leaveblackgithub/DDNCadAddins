using System.Threading;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using NUnit.Framework;
using ServiceACAD;

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

            void Action1(ITransactionService tr)
            {
                var line = new Line(new Point3d(0, 0, 0), new Point3d(100, 100, 100));

                var objectId = tr.AppendEntityToModelSpace(line);

                lineId = objectId.Handle.Value;
            }

            void Action2(ITransactionService tr)
            {
                //Check in another transaction if the line was created

                ObjectId objId;
                if (!CadServiceManager._.CadDb.TryGetObjectId(new Handle(lineId), out objId))
                {
                    Assert.Fail("Line didn't created");
                    return;
                }

                var line = tr.GetObject<Line>(objId);
                // line.Erase();
            }

            CadServiceManager._.ExecuteInTransactions("", Action1, Action2);
        }
    }
}
