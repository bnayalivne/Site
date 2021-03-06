﻿using System.IO;
using GeoAPI.Geometries;
using IsraelHiking.API.Controllers;
using IsraelHiking.API.Converters;
using IsraelHiking.API.Services;
using IsraelHiking.API.Services.Poi;
using IsraelHiking.Common;
using IsraelHiking.Common.Poi;
using IsraelHiking.DataAccessInterfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using OsmSharp.API;

namespace IsraelHiking.API.Tests.Controllers
{
    [TestClass]
    public class PointsOfInterestControllerTests
    {
        private PointsOfInterestController _controller;
        private IWikimediaCommonGateway _wikimediaCommonGateway;
        private IOsmGateway _osmGateway;
        private ITagsHelper _tagHelper;
        private IPointsOfInterestProvider _pointsOfInterestProvider;
        private IPointsOfInterestAggregatorService _pointsOfInterestAggregatorService;

        [TestInitialize]
        public void TestInitialize()
        {
            _pointsOfInterestProvider = Substitute.For<IPointsOfInterestProvider>();
            _pointsOfInterestAggregatorService = Substitute.For<IPointsOfInterestAggregatorService>();
            _tagHelper = Substitute.For<ITagsHelper>();
            _wikimediaCommonGateway = Substitute.For<IWikimediaCommonGateway>();
            _osmGateway = Substitute.For<IOsmGateway>();
            var cache = new LruCache<string, TokenAndSecret>(Substitute.For<IOptions<ConfigurationData>>(), Substitute.For<ILogger>());
            var factory = Substitute.For<IHttpGatewayFactory>();
            factory.CreateOsmGateway(Arg.Any<TokenAndSecret>()).Returns(_osmGateway);
            _controller = new PointsOfInterestController(factory, _tagHelper, _wikimediaCommonGateway, _pointsOfInterestProvider, _pointsOfInterestAggregatorService, new Base64ImageStringToFileConverter(),  cache);
        }

        [TestMethod]
        public void GetCategoriesByType_ShouldGetThemFromTagHelper()
        {
            var category = "category";

            _controller.GetCategoriesByType(category);

            _tagHelper.Received(1).GetCategoriesByType(category);
        }

        [TestMethod]
        public void GetPointsOfIntereset_NoCategory_ShouldReturnEmptyList()
        {
            var result = _controller.GetPointsOfInterest(string.Empty, string.Empty, string.Empty).Result;

            Assert.AreEqual(0, result.Length);
            _pointsOfInterestProvider.DidNotReceive().GetPointsOfInterest(Arg.Any<Coordinate>(), Arg.Any<Coordinate>(), Arg.Any<string[]>(), Arg.Any<string>());
        }

        [TestMethod]
        public void GetPointsOfIntereset_OneAdapter_ShouldReturnPoi()
        {
            _pointsOfInterestProvider.GetPointsOfInterest(Arg.Any<Coordinate>(), Arg.Any<Coordinate>(), Arg.Any<string[]>(),
                Arg.Any<string>()).Returns(new[] {new PointOfInterest()});

            var result = _controller.GetPointsOfInterest(string.Empty, string.Empty, "category", "language").Result;

            Assert.AreEqual(1, result.Length);
        }

        [TestMethod]
        public void GetPointOfIntereset_WrongSource_ShouldReturnBadRequest()
        {
            var result = _controller.GetPointOfInterest("wrong source", string.Empty, "language").Result as NotFoundResult;

            Assert.IsNotNull(result);
        }

        [TestMethod]
        public void GetPointOfInteresetCoordinates_BySourceAndId_ShouldReturnIt()
        {
            var id = "32_35";
            var source = Sources.COORDINATES;
            var language = "language";

            var result = _controller.GetPointOfInterest(source, id, language).Result as OkObjectResult;

            Assert.IsNotNull(result);
            var poi = result.Value as SearchResultsPointOfInterest;
            Assert.IsNotNull(poi);
            Assert.AreEqual(32, poi.Location.Lat);
        }

        [TestMethod]
        public void GetPointOfIntereset_BySourceAndId_ShouldReturnIt()
        {
            var id = "way_1";
            var source = "source";
            var language = "language";
            _pointsOfInterestAggregatorService.Get(source, id, language).Returns(new PointOfInterestExtended());

            var result = _controller.GetPointOfInterest(source, id, language).Result as OkObjectResult;

            Assert.IsNotNull(result);
        }

        [TestMethod]
        public void UploadPointOfInterest_WrongSource_ShouldReturnBadRequest()
        {
            var poi = new PointOfInterestExtended {Source = "wrong source"};

            var result = _controller.UploadPointOfInterest(poi, "he").Result as BadRequestObjectResult;

            Assert.IsNotNull(result);
        }

        [TestMethod]
        public void UploadPointOfInterest_IdDoesNotExists_ShouldAdd()
        {
            _controller.SetupIdentity();
            var poi = new PointOfInterestExtended { Source = Sources.OSM, Id = "" };

            var result = _controller.UploadPointOfInterest(poi, "he").Result as OkObjectResult;

            Assert.IsNotNull(result);
            _pointsOfInterestProvider.Received(1).AddPointOfInterest(Arg.Any<PointOfInterestExtended>(), Arg.Any<TokenAndSecret>(), Arg.Any<string>());
        }

        [TestMethod]
        public void UploadPointOfInterest_IdExists_ShouldUpdate()
        {
            _controller.SetupIdentity();
            var poi = new PointOfInterestExtended { Source = Sources.OSM, Id = "1" };

            var result = _controller.UploadPointOfInterest(poi, "he").Result as OkObjectResult;

            Assert.IsNotNull(result);
            _pointsOfInterestProvider.Received(1).UpdatePointOfInterest(Arg.Any<PointOfInterestExtended>(), Arg.Any<TokenAndSecret>(), Arg.Any<string>());
        }

        [TestMethod]
        public void UploadPointOfInterest_WithImageIdExists_ShouldUpdate()
        {
            var user = new User {DisplayName = "DisplayName"};
            _controller.SetupIdentity();
            _osmGateway.GetUser().Returns(user);
            var poi = new PointOfInterestExtended
            {
                Title = "title",
                Source = Sources.OSM,
                Id = "1",
                Location = new LatLng(5, 6),
                ImagesUrls = new [] { "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAUAAAAFCAYAAACNbyblAAAAHElEQVQI12P4//" +
                                      "8/w38GIAXDIBKE0DHxgljNBAAO9TXL0Y4OHwAAAABJRU5ErkJggg==", "http://link.com"}
            };
            _controller.UploadPointOfInterest(poi, "he").Wait();

            _wikimediaCommonGateway.Received(1).UploadImage(poi.Title, poi.Description, user.DisplayName, "title.png", Arg.Any<Stream>(), Arg.Any<Coordinate>());
            _wikimediaCommonGateway.Received(1).GetImageUrl(Arg.Any<string>());
        }
    }
}
