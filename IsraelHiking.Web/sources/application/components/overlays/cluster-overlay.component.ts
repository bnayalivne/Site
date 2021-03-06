﻿import { Component, Input, Output, EventEmitter } from "@angular/core";
import { Router } from "@angular/router";

import { ResourcesService } from "../../services/resources.service";
import { RouteStrings } from "../../services/hash.service";
import { PointOfInterest } from "../../models/models";
import { BaseMapComponent } from "../base-map.component";

@Component({
    selector: "cluster-overlay",
    templateUrl: "./cluster-overlay.component.html"
})
export class ClusterOverlayComponent extends BaseMapComponent {

    @Input()
    public points: PointOfInterest[];

    @Output()
    public close: EventEmitter<void>;

    constructor(resources: ResourcesService,
        private readonly router: Router) {
        super(resources);
        this.close = new EventEmitter();
    }

    public clickOnItem(point: PointOfInterest) {
        this.close.emit();
        this.router.navigate([RouteStrings.POI, point.source, point.id],
            { queryParams: { language: this.resources.getCurrentLanguageCodeSimplified() } });
    }
}