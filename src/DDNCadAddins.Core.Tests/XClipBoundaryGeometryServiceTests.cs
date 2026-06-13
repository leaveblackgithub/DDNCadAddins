using System.Collections.Generic;
using DDNCadAddins.Core.Models;
using DDNCadAddins.Core.Services;
using NUnit.Framework;

namespace DDNCadAddins.Core.Tests
{
    [TestFixture]
    public class XClipBoundaryGeometryServiceTests
    {
        private XClipBoundaryGeometryService _service;

        [SetUp]
        public void SetUp()
        {
            _service = new XClipBoundaryGeometryService();
        }

        [Test]
        public void BuildWcsBoundaryPoints_IdentityTransform_PreservesPoints()
        {
            var localPoints = new List<Point2D>
            {
                new Point2D(0, 0),
                new Point2D(10, 0),
                new Point2D(10, 5),
                new Point2D(0, 5)
            };

            var result = _service.BuildWcsBoundaryPoints(
                localPoints,
                Matrix3D.Identity,
                Matrix3D.Identity,
                Matrix3D.Identity);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(4, result.Data.Count);
            Assert.AreEqual(0, result.Data[0].X, 1e-9);
            Assert.AreEqual(10, result.Data[1].X, 1e-9);
        }

        [Test]
        public void BuildWcsBoundaryPoints_TwoPointRectangle_ExpandsToFour()
        {
            var localPoints = new List<Point2D>
            {
                new Point2D(1, 2),
                new Point2D(4, 6)
            };

            var result = _service.BuildWcsBoundaryPoints(
                localPoints,
                Matrix3D.Identity,
                Matrix3D.Identity,
                Matrix3D.Identity);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(4, result.Data.Count);
            Assert.AreEqual(1, result.Data[0].X, 1e-9);
            Assert.AreEqual(2, result.Data[0].Y, 1e-9);
            Assert.AreEqual(4, result.Data[2].X, 1e-9);
            Assert.AreEqual(6, result.Data[2].Y, 1e-9);
        }

        [Test]
        public void BuildWcsBoundaryPoints_Translation_AppliesOffset()
        {
            var localPoints = new List<Point2D> { new Point2D(0, 0), new Point2D(1, 1) };
            var translation = Matrix3D.FromArray(new[]
            {
                1d, 0d, 0d, 5d,
                0d, 1d, 0d, 6d,
                0d, 0d, 1d, 0d,
                0d, 0d, 0d, 1d
            });

            var result = _service.BuildWcsBoundaryPoints(
                localPoints,
                translation,
                Matrix3D.Identity,
                Matrix3D.Identity);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(5, result.Data[0].X, 1e-9);
            Assert.AreEqual(6, result.Data[0].Y, 1e-9);
        }

        [Test]
        public void BuildWcsBoundaryPoints_EmptyPoints_ReturnsFail()
        {
            var result = _service.BuildWcsBoundaryPoints(
                new List<Point2D>(),
                Matrix3D.Identity,
                Matrix3D.Identity,
                Matrix3D.Identity);

            Assert.IsFalse(result.IsSuccess);
        }

        [Test]
        public void PreMultiplyBy_TranslationChain_MatchesAutoCadOrder()
        {
            var clipSpace = Matrix3D.Identity;
            var originalInverse = Matrix3D.FromArray(new[]
            {
                1d, 0d, 0d, 1d,
                0d, 1d, 0d, 2d,
                0d, 0d, 1d, 0d,
                0d, 0d, 0d, 1d
            });
            var blockTransform = Matrix3D.FromArray(new[]
            {
                1d, 0d, 0d, 10d,
                0d, 1d, 0d, 20d,
                0d, 0d, 1d, 0d,
                0d, 0d, 0d, 1d
            });

            var combined = clipSpace.PreMultiplyBy(originalInverse).PreMultiplyBy(blockTransform);
            var transformed = combined.TransformPlanarPoint(new Point2D(0, 0));

            Assert.AreEqual(11, transformed.X, 1e-9);
            Assert.AreEqual(22, transformed.Y, 1e-9);
        }

        [Test]
        public void ExpandLocalBoundaryPoints_ThreePoints_KeepsOriginalCount()
        {
            var localPoints = new List<Point2D>
            {
                new Point2D(0, 0),
                new Point2D(1, 0),
                new Point2D(1, 1)
            };

            var expanded = XClipBoundaryGeometryService.ExpandLocalBoundaryPoints(localPoints);

            Assert.AreEqual(3, expanded.Count);
        }
    }
}
