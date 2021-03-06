﻿using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using GeoAPI.Geometries;
using IsraelHiking.API.Controllers;
using IsraelHiking.API.Converters.ConverterFlows;
using IsraelHiking.API.Gpx;
using IsraelHiking.API.Services;
using IsraelHiking.Common;
using IsraelHiking.DataAccessInterfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NSubstitute;

namespace IsraelHiking.API.Tests.Controllers
{
    [TestClass]
    public class CsvControllerTests
    {
        private CsvController _controller;
        private IHttpGatewayFactory _httpGatewayFactory;
        private IDataContainerConverterService _dataContainerConverterService;

        [TestInitialize]
        public void TestInitialize()
        {
            _httpGatewayFactory = Substitute.For<IHttpGatewayFactory>();
            _dataContainerConverterService = Substitute.For<IDataContainerConverterService>();
            _controller = new CsvController(_dataContainerConverterService, _httpGatewayFactory);
        }

        [TestMethod]
        public void UploadCsv_ShouldConvertItAndAddMissingFields()
        {
            IFormFile file = Substitute.For<IFormFile>();
            file.OpenReadStream()
                .Returns(new MemoryStream(Encoding.UTF8.GetBytes(
                    "Title,Description,Website,ImageUrl,FileUrl\r\ntitle,description,website?id=42,image,file")));
            var fetcher = Substitute.For<IRemoteFileFetcherGateway>();
            fetcher.GetFileContent("file").Returns(new RemoteFileFetcherGatewayResponse());
            _httpGatewayFactory.CreateRemoteFileFetcherGateway(null).Returns(fetcher);
            var featureCollection = new FeatureCollection(new Collection<IFeature>
            {
                new Feature(new Point(new Coordinate(11, 12)), new AttributesTable())
            });
            _dataContainerConverterService.Convert(Arg.Any<byte[]>(), Arg.Any<string>(), FlowFormats.GEOJSON)
                .Returns(featureCollection.ToBytes());

            var resutls = _controller.UploadCsv(file, "\\?id=(.*)", "http://sourceImageUrl/1.png", "icon", "icon-color", Categories.ROUTE_HIKE).Result as FileStreamResult;

            Assert.IsNotNull(resutls);
            var memoryStream = new MemoryStream();
            resutls.FileStream.CopyTo(memoryStream);
            var resutlsString = Encoding.UTF8.GetString(memoryStream.ToArray());
            Assert.IsTrue(resutlsString.Contains("42"));
            Assert.IsTrue(resutlsString.Contains("11"));
            Assert.IsTrue(resutlsString.Contains("12"));
            Assert.IsTrue(resutlsString.Contains("icon"));
            Assert.IsTrue(resutlsString.Contains("icon-color"));
            Assert.IsTrue(resutlsString.Contains("http://sourceImageUrl/1.png"));
            Assert.IsTrue(resutlsString.Contains("Hiking"));
        }
    }
}
