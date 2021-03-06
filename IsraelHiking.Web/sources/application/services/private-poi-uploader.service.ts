﻿import { Injectable } from "@angular/core";
import { Router } from "@angular/router";
import { NgRedux } from "@angular-redux/store";
import { chain, flatten, map } from "lodash";

import { ResourcesService } from "./resources.service";
import { SnappingService } from "./snapping.service";
import { PoiService } from "./poi.service";
import { ToastService } from "./toast.service";
import { RouteStrings } from "./hash.service";
import { LinkData, LatLngAlt, MarkerData, ApplicationState } from "../models/models";
import { SetUploadMarkerDataAction } from "../reducres/poi.reducer";

@Injectable()
export class PrivatePoiUploaderService {
    constructor(private readonly router: Router,
        private readonly resources: ResourcesService,
        private readonly poiService: PoiService,
        private readonly snappingService: SnappingService,
        private readonly toastService: ToastService,
        private readonly ngRedux: NgRedux<ApplicationState>) {
    }

    public async uploadPoint(
        latLng: LatLngAlt,
        imageLink: LinkData,
        title: string,
        description: string,
        markerType: string
    ) {
        let results = await this.snappingService.getClosestPoint(latLng);
        let urls = [];
        if (imageLink) {
            urls = [imageLink];
        }
        let markerData = {
            description: description,
            title: title,
            latlng: latLng,
            type: markerType,
            urls: urls
        } as MarkerData;

        this.ngRedux.dispatch(new SetUploadMarkerDataAction({
            markerData: markerData
        }));

        if (results == null) {
            this.router.navigate([RouteStrings.ROUTE_POI, "new", ""],
                { queryParams: { language: this.resources.getCurrentLanguageCodeSimplified(), edit: true } });
            return;
        }
        let message = `${this.resources.wouldYouLikeToUpdate} ${results.title}?`;
        if (!results.title) {
            let categories = await this.poiService.getSelectableCategories();
            let iconWithLabel = chain(categories)
                .map(c => c.icons)
                .flatten()
                .find(i => i.icon === `icon-${results.type}`)
                .value();
            if (iconWithLabel) {
                let type = this.resources.translate(iconWithLabel.label);
                message = `${this.resources.wouldYouLikeToUpdate} ${type}?`;
            } else {
                message = this.resources.wouldYouLikeToUpdateThePointWithoutTheTitle;
            }
        }
        this.toastService.confirm({
            message: message,
            type: "YesNo",
            confirmAction: () => {
                this.router.navigate([RouteStrings.ROUTE_POI, "OSM", results.id],
                    { queryParams: { language: this.resources.getCurrentLanguageCodeSimplified(), edit: true } });
            },
            declineAction: () => {
                this.router.navigate([RouteStrings.ROUTE_POI, "new", ""],
                    { queryParams: { language: this.resources.getCurrentLanguageCodeSimplified(), edit: true } });
            }
        });
    }
}