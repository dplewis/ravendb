import viewModelBase = require("viewmodels/viewModelBase");
import getStatusStorageBreakdownCommand = require("commands/database/debug/getStatusStorageBreakdownCommand");
import killOperationComamnd = require('commands/operations/killOperationCommand');
import shell = require('viewmodels/shell');
import accessHelper = require("viewmodels/shell/accessHelper");
import eventsCollector = require("common/eventsCollector");

class statusStorageOnDisk extends viewModelBase {
    data = ko.observable<string[]>();
    isGlobalAdmin = accessHelper.isGlobalAdmin;
    canSearch = ko.observable(true);
    progress = ko.observable<string>();
    operationId = ko.observable<number>();

    formatToPreTag(input: string) {
        return input.replaceAll('\r\n', '<br />').replaceAll("\t", '&nbsp;&nbsp;&nbsp;&nbsp;');
    }

    cancelOperation() {
        eventsCollector.default.reportEvent("storage", "kill-breakdown");

        if (this.operationId()) {
            new killOperationComamnd(this.activeDatabase(), this.operationId())
                .execute();
        }
    }

    fetchData(): JQueryPromise<any> {
        eventsCollector.default.reportEvent("storage", "breakdown");

        var db = this.activeDatabase();
        if (db && this.isGlobalAdmin()) {
            this.canSearch(false);
            var command = new getStatusStorageBreakdownCommand(db, msg => this.progress(msg));
            return command
                .execute()
                .done((scheduleTaskResult: operationIdDto) => {
                    this.operationId(scheduleTaskResult.OperationId);
                    command.getBreakdownCompletedTask()
                        .done(result => {
                            this.data(result.map(this.formatToPreTag));
                        })
                        .always(() => {
                            this.operationId(null);
                            this.progress(null);
                            this.canSearch(true);
                        });
                })
                .fail(() => this.canSearch(true));
        }

        return null;
    }
}

export = statusStorageOnDisk;