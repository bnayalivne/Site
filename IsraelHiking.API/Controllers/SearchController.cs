﻿using System.Linq;
using System.Threading.Tasks;
using GeoAPI.Geometries;
using IsraelHiking.DataAccessInterfaces;
using NetTopologySuite.Features;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using IsraelHiking.API.Converters;
using IsraelHiking.API.Converters.CoordinatesParsers;
using IsraelHiking.API.Services.Poi;
using IsraelHiking.Common;
using IsraelHiking.Common.Extensions;
using IsraelHiking.Common.Poi;

namespace IsraelHiking.API.Controllers
{
    /// <summary>
    /// This contoller allows search of geo-locations
    /// </summary>
    [Route("api/[controller]")]
    public class SearchController : Controller
    {
        private readonly IElasticSearchGateway _elasticSearchGateway;
        private readonly IEnumerable<ICoordinatesParser> _coordinatesParsers;

        /// <summary>
        /// Controller's constructor
        /// </summary>
        /// <param name="elasticSearchGateway"></param>
        /// <param name="coordinatesParsers"></param>
        public SearchController(IElasticSearchGateway elasticSearchGateway,
            IEnumerable<ICoordinatesParser> coordinatesParsers)
        {
            _elasticSearchGateway = elasticSearchGateway;
            _coordinatesParsers = coordinatesParsers;
        }

        /// <summary>
        /// Gets a geo location by search term
        /// </summary>
        /// <param name="term">A string to search for</param>
        /// <param name="language">The language to search in</param>
        /// <returns></returns>
        // GET api/search/abc&language=en
        [HttpGet]
        [Route("{term}")]
        public async Task<IEnumerable<SearchResultsPointOfInterest>> GetSearchResults(string term, string language)
        {
            var coordinates = GetCoordinates(term.Trim());
            if (coordinates != null)
            {
                return new[] {ConvertFromCoordinates(term, coordinates)};
            }
            
            if (term.Count(c => c == ',') == 1)
            {
                var splitted = term.Split(',');
                var place = splitted.Last().Trim();
                term = splitted.First().Trim();
                var placesFeatures = await _elasticSearchGateway.SearchPlaces(place, language);
                if (placesFeatures.Any())
                {
                    var envolope = placesFeatures.First().Geometry.EnvelopeInternal;
                    var featuresWithinPlaces = await _elasticSearchGateway.SearchByLocation(
                        new Coordinate(envolope.MaxX, envolope.MaxY), new Coordinate(envolope.MinX, envolope.MinY), term, language);
                    return await Task.WhenAll(featuresWithinPlaces.OfType<IFeature>().ToList().Select(f => ConvertFromFeature(f,language)));
                }
            }
            var features = await _elasticSearchGateway.Search(term, language);
            return await Task.WhenAll(features.OfType<IFeature>().ToList().Select(f => ConvertFromFeature(f, language)));
        }

        private Coordinate GetCoordinates(string term)
        {
            return _coordinatesParsers.Select(parser => parser.TryParse(term))
                .FirstOrDefault(coordinates => coordinates != null);
        }

        private SearchResultsPointOfInterest ConvertFromCoordinates(string name, Coordinate coordinates)
        {
            var latLng = new LatLng(coordinates.Y, coordinates.X, coordinates.Z);
            return CoordinatesToPointOfInterestConverter.Convert(latLng, name);
        }

        private async Task<SearchResultsPointOfInterest> ConvertFromFeature(IFeature feature, string language)
        {
            var title = feature.GetTitle(language);
            var geoLocation = (AttributesTable)feature.Attributes[FeatureAttributes.GEOLOCATION];
            var latLng = new LatLng((double)geoLocation[FeatureAttributes.LAT], (double)geoLocation[FeatureAttributes.LON]);
            var icon = feature.Attributes[FeatureAttributes.ICON].ToString();
            if (string.IsNullOrWhiteSpace(icon))
            {
                icon = OsmPointsOfInterestAdapter.SEARCH_ICON;
            }
            return new SearchResultsPointOfInterest
            {
                Id = feature.Attributes[FeatureAttributes.ID].ToString(),
                Title = title,
                DisplayName = await GetDisplayName(feature, language, title),
                Category = feature.Attributes[FeatureAttributes.POI_CATEGORY].ToString(),
                Icon = icon,
                IconColor = feature.Attributes[FeatureAttributes.ICON_COLOR].ToString(),
                Source = feature.Attributes[FeatureAttributes.POI_SOURCE].ToString(),
                Location = latLng,
                HasExtraData = feature.HasExtraData(language),
                NorthEast = new LatLng(feature.Geometry.EnvelopeInternal.MaxY, feature.Geometry.EnvelopeInternal.MaxX),
                SouthWest = new LatLng(feature.Geometry.EnvelopeInternal.MinY, feature.Geometry.EnvelopeInternal.MinX)
            };
        }

        private async Task<string> GetDisplayName(IFeature feature, string language, string title)
        {
            var displayName = title;
            var containers = await _elasticSearchGateway.GetContainers(feature.Geometry.Coordinate);
            var featureGeometry = feature.Geometry.GetGeometryN(0);
            var container = containers.Where(c =>
                    c.Attributes[FeatureAttributes.ID] != feature.Attributes[FeatureAttributes.ID] &&
                    c.Geometry.Covers(featureGeometry) &&
                    c.Geometry.EqualsTopologically(featureGeometry) == false)
                .OrderBy(c => c.Geometry.Area)
                .FirstOrDefault();
            if (container == null)
            {
                return displayName;
            }
            var address = container.GetTitle(language);
            if (!string.IsNullOrWhiteSpace(address))
            {
                displayName += ", " + address;
            }

            return displayName;
        }
    }
}
