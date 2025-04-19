using System.Threading;
using System.Windows.Forms;
using Autodesk.AutoCAD.DatabaseServices;
using NUnit.Framework;
using TestRunnerACAD;

namespace AddinsACAD.TestInApp
{
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class TestInAppOnly
    {
        [Test]
        public void Test_InAppOnly()
        {
            void Action1(Database db, Transaction tr)
            {
                MessageBox.Show("app only");
            }

            // 使用统一的ExecuteInAny方法，自动选择合适的执行方式
            TestUtils.ExecuteDbActions(null, Action1);
        }
    }
}
