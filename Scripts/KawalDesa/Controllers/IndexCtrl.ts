﻿/// <reference path="../../../Scaffold/Scripts/typings/angularjs/angular.d.ts"/>
/// <reference path="../../gen/Models.ts"/>
/// <reference path="../KawalDesa.ts"/>


module App.Controllers {

    function safeApply(scope, fn) {
        (scope.$$phase || scope.$root.$$phase) ? fn() : scope.$apply(fn);
    }

    export interface ICurrentUser {
        Id: string;
        FacebookId: String;
        Name: String;
        fkOrganizationId: number;
        Roles: string[];
        Scopes: string[];
    }

    interface MyWindow extends Window {
        CurrentUser: ICurrentUser;
    }

    declare var window: MyWindow;

    import Models = App.Models;
    import Controllers = App.Controllers.Models;

    var CHILD_NAMES = [
        "Daerah",
        "Provinsi",
        "Kabupaten / Kota",
        "Kecamatan",
        "Desa"
    ];

    /* a route is a [prefix, type, useRegionId]
     *  prefix: the url prefix, 
     *  type: the indexCtrl type if this route matches, 
     *  useRegionId: whether the rout param use region id
     */
    var ROUTES : any[][] = [
        ["/dd/", "dd", true],
        ["/add/", "add", true],
        ["/bhpr/", "bhpr", true],
        ["/p/", "transfer", true],
        ["/r/", "realization", true],
        ["/dashboard", "dashboard", false],
        ["/orgs", "orgs", false],
        ["/u/", "users", false],
        ["/login", "login", false],
    ];

    export class IndexCtrl {

        region: Models.Region;
        regionId: string;
        regionTree: Models.Region[];

        type = "transfer";
        isPathReplacing = false;
        currentPath : string = null;

        childName: string;
        guessedRegionType: number;

        currentUser: ICurrentUser;

        static $inject = ["$scope", "$location", "$modal", "$document"];

        constructor(public $scope, public $location: ng.ILocationService, public $modal, public $document){
            $scope.App = App;
            var ctrl = this;
            var scope = this.$scope;
            this.currentUser = window.CurrentUser;

            /* Path replacing occurs when there is a redirection, e.g /r/21121212 to /mandalmekar */
            if(!ctrl.isPathReplacing)
                ctrl.onLocationChange();
            ctrl.isPathReplacing = false;

            $scope.$on('$locationChangeSuccess', function () {
                if(!ctrl.isPathReplacing)
                    ctrl.onLocationChange();
                ctrl.isPathReplacing = false;
            });

            //dropdowns
            $document.bind('click', function () {
                var dropdownStatusVars = ["navOpen", "navUserOpen"];
                for (var i = 0; i < dropdownStatusVars.length; i++) {
                    var ddVar = dropdownStatusVars[i];
                    if (ctrl.$scope[ddVar]) {
                        $scope.$apply(() => {
                            ctrl.$scope[ddVar] = false;
                        });
                    }
                }
            });
        }

        onLocationChange(): void {
            var path = this.$location.path();
            if (path == this.currentPath)
                return;

            var regionId:string = null;
            var regionKey = null;

            this.type = null;
            if (path == "/" || path == "") {
                regionId = "0";
                this.type = "transfer";
            } else {
                var matched : any[] = ROUTES.filter(r => path.indexOf(r[0]) == 0);
                if (matched[0]) {
                    this.type = matched[0][1] ;
                    if (matched[0][2]) {
                        regionId = path.replace(matched[0][0], "");
                    }
                }
            }
            if (this.type == null) {
                this.type = "realization";
                regionKey = path.substring(1);
            }

            this.guessedRegionType = this.guessType(regionId);

            if(regionId != null || regionKey)
                this.loadRegion(regionId, regionKey);
            if (regionId == null && !regionKey)
                regionId = "0";

            this.regionId = regionId;
            this.currentPath = path;
        }

        guessType(regionId: string): number {
            if (regionId == "0")
                return 0;
            if (!regionId)
                return null;
            return (regionId.match(/\./g) || []).length + 1;
        }

        changeType(type: string, $event): void {

            for (var i = 0, len = ROUTES.length; i < len; i++) {
                var route = ROUTES[i];
                var useRegionId = route[2];
                var routeType = route[1];
                if (this.type == routeType && !useRegionId) {
                    return;
                }
            }

            if (this.type != 'dashboard') {
                $event.preventDefault();
                var matched : any[] = ROUTES.filter(r => r[1] == type);
                var regionId = this.region.Id;
                if (this.region.Type == 4 && type != "transfer")
                    regionId = this.region.fkParentId;
                var path = matched[0][0] + regionId;
                this.$location.path(path);
            }
        }

        changeRegion(regionId: string, $event): void {
            $event.preventDefault();
            var type = this.type;
            var matched : any[] = ROUTES.filter(r => r[1] == type);
            var path = matched[0][0] + regionId;
            this.$location.path(path);
        }

        loadRegion(parentId?: string, parentKey?: string) {
            setTimeout(() => {
                ctrl.$scope.$apply(() => {
                this.$scope.$broadcast("regionChangeBefore");
                });
            }, 0);
            var ctrl = this;

            this.regionTree = [];
            this.childName = CHILD_NAMES[0];

            var promise = null;
            if (parentId != null)
                promise = Controllers.RegionController.Get(parentId);
            else if (parentKey)
                promise = Controllers.RegionController.GetByURLKey(parentKey);

            promise.then((regionData: ng.IHttpPromiseCallbackArg<Models.Region>) => {
                    var region = regionData.data;
                    ctrl.region = region;
                    ctrl.regionId = region.Id;

                    ctrl.initSourceUploadDefaults();

                    var regionTree = [];
                    var cur : Models.IRegion = region;
                    while (cur) {
                        regionTree.push(cur);
                        cur = cur.Parent;
                    }
                    ctrl.regionTree = regionTree.reverse();
                    if (regionTree.length < CHILD_NAMES.length)
                        ctrl.childName = CHILD_NAMES[regionTree.length];

                    if (region.UrlKey && ctrl.$location.path() != "/" + region.UrlKey) {
                        ctrl.isPathReplacing = true;
                        ctrl.$location.path("/" + region.UrlKey);
                        ctrl.$location.replace();
                    }

                    setTimeout(() => {
                        ctrl.$scope.$apply(() => {
                            ctrl.$scope.$broadcast("regionChangeSuccess");
                        });
                    }, 0);

            });
        }

        /* UI Utils */

        modalInstance: any;

        modal(template: string, $scope?): void {
            var ctrl = this;
            ctrl.modalInstance = this.$modal.open({
                templateUrl: template,
                scope: $scope ? $scope : this.$scope
            });
        }

        closeModal(): void {
            if (this.modalInstance) {
                this.modalInstance.close();
                this.modalInstance = null;
            }
        }

        getBarPercent(value: number, fullValue: number): {} {
            var percent = value * 100 / fullValue;
            return { "width": percent + "%" };
        }


        /* Security Utils */

        isInRole(roleName: string): boolean {
            if (!this.currentUser) {
                return false;
            }
            return this.currentUser.Roles.some(r => roleName == r);
        }

        isInScope(entityId: string): boolean {
            var regionId = this.regionTree.map(r => r.Id);
            regionId.push(entityId);
            return regionId.some(rid => this.currentUser.Scopes.some(id => rid == id));
        }

        isInRoleAndScope(roleName: string, entityId: string): boolean {
            return this.isInRole(roleName) && this.isInScope(entityId);
        }

        hasAnyVolunteerRoles(): boolean {
            return this.currentUser != null
                && this.currentUser.Roles.some(r => r.indexOf("volunteer_") != -1);
        }


        /* Google Sheet */

        isLoadingUrl = false;

        openGoogleSheet() : void {
            if (this.isLoadingUrl)
                return;
            var ctrl = this;
            this.isLoadingUrl = true;
            Controllers.SpreadsheetController
                .GetCurrentSheetUrl(this.activeUploadType, this.activeUploadRegionId, "2015p")
                .then(url => {
                    ctrl.isLoadingUrl = false;
                    window.open(url.data, "_blank");
                });
        }


        /* Search */

        showSearch() : void {
            this.$scope.searchShown = true;
            setTimeout(function () {
                $(".search-input-group input").focus();
                $(".search-input-group input").select();
            }, 0);
        }

        searchRegions(keyword, scopeToFunction?) : any {
            var query = { "keyword": keyword };
            if (scopeToFunction)
                query["function"]= this.newSourceFunction;
            return Controllers.RegionSearchResultController.GetAll(query)
                .then((regions) => regions.data);
        }

        onSearchSelected(item, model) : void {
            var type = this.type;
            var regionId = model.Type == 4 && type !== 'transfer' ? model.ParentId : model.Id;
            var matched : any[] = ROUTES.filter(r => r[1] == type);
            var path = matched[0][0] + regionId;
            this.$location.path(path);
        }


        /* Upload */

        activeUploadType: App.Models.DocumentUploadType;
        activeUploadRegionId: string;
        activeUpload: App.Models.Spreadsheet;
        activeUploadLoading= true;

        activeSources:  App.Models.SourceDocument[];
        newSourceFile: any;
        newSourceSubType: string;
        newSourceDate: string;
        newSourceAmount: number;
        newSourceFunction: App.Models.SourceDocumentFunction;
        newSourceRegion: any;
        newSourceState = false;

        configureDocumentUpload(type: App.Models.DocumentUploadType, regionId: string) : void {
            this.activeUploadType = type;
            this.activeUploadRegionId = regionId;
            var ctrl = this;
            ctrl.activeUpload = null;
            ctrl.activeUploadLoading = true;
            Controllers.SpreadsheetController
                .GetActive(type, regionId, "2015p")
                .then(doc => {
                    ctrl.activeUpload = doc.data;
                    ctrl.activeUploadLoading = false;
                    Controllers.SourceDocumentController
                        .GetAll({ "fkRegionId": regionId, "type": type, "apbnKey": "2015p" })
                        .then(sources => {
                            ctrl.activeSources = sources.data;
                    });
            });
        }

        initSourceUploadDefaults() : void {
            var ctrl = this;
            var region = ctrl.region;

            ctrl.newSourceRegion = region;
            if (ctrl.newSourceFunction == Models.SourceDocumentFunction.Allocation) {
                if (region.Type == 1 || region.Type == 3)
                    ctrl.newSourceRegion = region.Parent;
                if (region.Type == 4)
                    ctrl.newSourceRegion = region.Parent.Parent;
            } else {
                if (region.Type != 4)
                    ctrl.newSourceRegion = null;
            }
            ctrl.newSourceFunction = ctrl.type == "transfer"
                ? Models.SourceDocumentFunction.Transfer
                : Models.SourceDocumentFunction.Allocation;
            if (ctrl.type == "dd")
                ctrl.newSourceSubType = "Dd";
            if (ctrl.type == "add")
                ctrl.newSourceSubType = "Add";
            if (ctrl.type == "bhpr")
                ctrl.newSourceSubType = "Bhpr";
        }

        uploadSource() : void {
            var typeStr = this.newSourceRegion.Id == "0" ? "National" : "Regional";
            typeStr = typeStr + this.newSourceSubType;
            var type = Models.DocumentUploadType[typeStr];
            var fn = this.newSourceFunction;

            var ctrl = this;
            var multipart = new Scaffold.Multipart({ files: this.newSourceFile });

            if (fn == Models.SourceDocumentFunction.Transfer) {
                var form = new Models.Transfer();
                var date = ctrl.newSourceDate;
                date = date.substr(6, 4) + "-"
                        + date.substr(3, 2) + "-"
                        + date.substr(0, 2) + "T00:00:00";
                form.Date= date;
                if (this.newSourceSubType == "Dd")
                    form.Dd = this.newSourceAmount;
                else if (this.newSourceSubType == "Add")
                    form.Dd = this.newSourceAmount;
                else if (this.newSourceSubType == "Bhpr")
                    form.Bhpr = this.newSourceAmount;
                multipart["forms"] = form;
            }


            ctrl.newSourceState = true;
            Controllers.SourceDocumentController
                .Upload(multipart, type, fn, this.newSourceRegion.Id, "2015p")
                .success(() => {
                    safeApply(ctrl.$scope, () => {
                        ctrl.closeModal();
                        ctrl.configureDocumentUpload(ctrl.activeUploadType, ctrl.activeUploadRegionId);
                    });
                }).finally(() => {
                    safeApply(ctrl.$scope, () => {
                        ctrl.newSourceState = false;
                });
            });;
        }


    }

    kawaldesa.controller("IndexCtrl", IndexCtrl);
}