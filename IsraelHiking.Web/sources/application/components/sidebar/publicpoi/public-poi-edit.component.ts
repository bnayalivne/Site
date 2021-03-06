import { Component, Input, OnInit } from "@angular/core";
import { MatSelectChange } from "@angular/material";

import { PoiService, ISelectableCategory, IIconColorLabel } from "../../../services/poi.service";
import { BaseMapComponent } from "../../base-map.component";
import { ResourcesService } from "../../../services/resources.service";
import { PointOfInterestExtended } from "../../../models/models";


@Component({
    selector: "public-poi-edit",
    templateUrl: "./public-poi-edit.component.html",
    styleUrls: ["./public-poi-edit.component.scss"]
})
export class PublicPointOfInterestEditComponent extends BaseMapComponent implements OnInit {
    @Input()
    public info: PointOfInterestExtended;

    public categories: ISelectableCategory[];
    public selectedCategory: ISelectableCategory;

    constructor(resources: ResourcesService,
        private readonly poiService: PoiService) {
        super(resources);
        this.selectedCategory = null;
        this.categories = [];
    }

    private async initializeCategories() {
        let categories = await this.poiService.getSelectableCategories();
        for (let category of categories) {
            this.categories.push(category);
        }
    }

    public async ngOnInit(): Promise<void> {
        await this.initializeCategories();
        for (let category of this.categories) {
            let icon = category.icons.find(iconToFind => iconToFind.icon === this.info.icon);
            if (icon) {
                this.selectCategory({ value: category } as MatSelectChange);
                this.selectIcon(icon);
            }
        }

        if (this.selectedCategory == null) {
            let category = this.categories.find(categoryToFind => categoryToFind.name === "Other");
            let icon = { icon: this.info.icon, color: "black", label: this.resources.other } as IIconColorLabel;
            category.icons.push(icon);
            this.selectCategory({ value: category } as MatSelectChange);
            this.selectIcon(icon);
        }

        if (this.info.references.length === 0) {
            this.addEmptyUrl();
        }
    }

    public selectCategory(e: MatSelectChange) {
        this.categories.forEach(c => c.isSelected = false);
        this.selectedCategory = e.value;
        this.selectedCategory.isSelected = true;
        if (this.selectedCategory.selectedIcon == null) {
            this.selectIcon(this.selectedCategory.icons[0]);
        }
    }

    public selectIcon(icon: IIconColorLabel) {
        this.selectedCategory.selectedIcon = icon;
        this.info.icon = icon.icon;
    }

    public addEmptyUrl() {
        this.info.references.push({url: "", sourceImageUrl: ""});
    }

    public removeUrl(i: number) {
        this.info.references.splice(i, 1);
    }

    public trackByIndex(index) {
        return index;
    }
}