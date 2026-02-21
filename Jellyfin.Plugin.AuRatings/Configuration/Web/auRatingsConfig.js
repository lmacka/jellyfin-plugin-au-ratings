(function () {
    'use strict';

    var pluginId = 'b4c7d8e9-2345-6789-bcde-fa0123456789';

    document.querySelector('#AuRatingsSettingsPage')
        .addEventListener('pageshow', function () {
            Dashboard.showLoadingMsg();
            ApiClient.getPluginConfiguration(pluginId).then(function (config) {
                document.querySelector('#settingsPerPage').value = config.ItemsPerPage;
                document.querySelector('#settingsView').value = config.DefaultView;
                Dashboard.hideLoadingMsg();
            });
        });

    document.querySelector('#AuRatingsSettingsForm')
        .addEventListener('submit', function (e) {
            Dashboard.showLoadingMsg();
            ApiClient.getPluginConfiguration(pluginId).then(function (config) {
                config.ItemsPerPage = parseInt(document.querySelector('#settingsPerPage').value, 10);
                config.DefaultView = document.querySelector('#settingsView').value;
                ApiClient.updatePluginConfiguration(pluginId, config).then(function (result) {
                    Dashboard.processPluginConfigurationUpdateResult(result);
                });
            });

            e.preventDefault();
            return false;
        });
})();
