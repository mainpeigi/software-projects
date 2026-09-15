// ECharts Interop for Blazor
window.echartsInterop = {
    charts: {},

    // Initialize or update a chart
    initOrUpdateChart: function(chartId, optionsJson, themeParam, syncChartsJson, tooltipFormatter, decimalPlaces) {
        console.log('[ECharts] initOrUpdateChart called for:', chartId);
        return new Promise((resolve, reject) => {
            window.waitForEcharts(function() {
                try {
                    console.log('[ECharts] ECharts loaded, initializing chart:', chartId);
                    var chartDom = document.getElementById(chartId);
                    if (!chartDom) {
                        console.error('Chart DOM element not found:', chartId);
                        reject('Chart DOM not found');
                        return;
                    }
                    console.log('[ECharts] Chart DOM found:', chartId);

                    // Get or create chart instance
                    var myChart = echarts.getInstanceByDom(chartDom);
                    if (!myChart) {
                        var theme = themeParam === 'null' ? null : themeParam.replace(/'/g, '');
                        myChart = echarts.init(chartDom, theme, {
                            renderer: 'canvas',
                            useDirtyRect: true
                        });
                    } else {
                        myChart.dispose();
                        var theme = themeParam === 'null' ? null : themeParam.replace(/'/g, '');
                        myChart = echarts.init(chartDom, theme, {
                            renderer: 'canvas',
                            useDirtyRect: true
                        });
                    }

                    // Parse chart options
                    var chartOption = JSON.parse(optionsJson);

                    // Set tooltip formatter
                    var formatterBody = tooltipFormatter.trim();
                    if (formatterBody.startsWith('function')) {
                        var firstBrace = formatterBody.indexOf('{');
                        var lastBrace = formatterBody.lastIndexOf('}');
                        if (firstBrace !== -1 && lastBrace !== -1 && lastBrace > firstBrace) {
                            formatterBody = formatterBody.substring(firstBrace + 1, lastBrace).trim();
                        }
                    }
                    chartOption.tooltip.formatter = new Function('params', formatterBody);

                    // Add Y-axis label formatter for decimal places
                    if (chartOption.yAxis) {
                        var formatValue = function(value, decimals) {
                            if (Math.abs(value) >= 1000000 || (Math.abs(value) < 0.01 && value !== 0)) {
                                return value.toExponential(decimals);
                            }
                            return value.toFixed(decimals);
                        };

                        if (Array.isArray(chartOption.yAxis)) {
                            chartOption.yAxis.forEach(axis => {
                                if (axis.axisLabel) {
                                    axis.axisLabel.formatter = function(value) {
                                        return formatValue(value, decimalPlaces);
                                    };
                                }
                            });
                        } else if (chartOption.yAxis.axisLabel) {
                            chartOption.yAxis.axisLabel.formatter = function(value) {
                                return formatValue(value, decimalPlaces);
                            };
                        }
                    }

                    // Set chart options
                    console.log('[ECharts] Setting chart options for:', chartId);
                    myChart.setOption(chartOption, {
                        notMerge: true,
                        lazyUpdate: true,
                        silent: false
                    });
                    console.log('[ECharts] Chart options set successfully for:', chartId);

                    // Setup synchronized zooming
                    if (!window.zoomSyncGroup) {
                        window.zoomSyncGroup = {
                            charts: {},
                            syncCharts: [],
                            isUpdating: false
                        };
                        console.log('[DataZoom] Initialized zoomSyncGroup');
                    }

                    // Register this chart instance
                    window.zoomSyncGroup.charts[chartId] = myChart;
                    console.log('[DataZoom] Registered chart:', chartId);

                    // Update the sync charts array
                    window.zoomSyncGroup.syncCharts = JSON.parse(syncChartsJson);
                    console.log('[DataZoom] Updated syncCharts array:', window.zoomSyncGroup.syncCharts);

                    // Setup dataZoom event handler
                    myChart.off('dataZoom');
                    myChart.on('dataZoom', function(params) {
                        console.log('[DataZoom] Event fired on', chartId, params);

                        if (window.zoomSyncGroup.isUpdating) {
                            console.log('[DataZoom] Skipping - already updating');
                            return;
                        }

                        // Extract chart number
                        var chartNumStr = chartId.replace('chart', '');
                        var chartNum = chartNumStr === '' ? 0 : parseInt(chartNumStr);
                        console.log('[DataZoom] Source chart number:', chartNum);

                        // Check if this chart is in the sync group
                        if (!window.zoomSyncGroup.syncCharts.includes(chartNum)) {
                            console.log('[DataZoom] Chart', chartNum, 'not in sync group', window.zoomSyncGroup.syncCharts);
                            return;
                        }

                        console.log('[DataZoom] Chart', chartNum, 'IS in sync group - proceeding with sync');

                        // Set flag to prevent circular events
                        window.zoomSyncGroup.isUpdating = true;

                        try {
                            // Get the zoom range
                            var start, end;

                            if (params && params.batch && params.batch.length > 0) {
                                start = params.batch[0].start;
                                end = params.batch[0].end;
                            } else if (params && params.start !== undefined) {
                                start = params.start;
                                end = params.end;
                            } else {
                                var option = myChart.getOption();
                                if (!option.dataZoom || option.dataZoom.length === 0) {
                                    console.log('[DataZoom] No dataZoom config found');
                                    return;
                                }
                                start = option.dataZoom[0].start;
                                end = option.dataZoom[0].end;
                            }

                            console.log('[DataZoom] Zoom range:', start, '-', end);

                            // Update all other synced charts
                            var updatedCount = 0;
                            for (var targetChartId in window.zoomSyncGroup.charts) {
                                if (targetChartId === chartId) continue;

                                var targetChartNumStr = targetChartId.replace('chart', '');
                                var targetChartNum = targetChartNumStr === '' ? 0 : parseInt(targetChartNumStr);

                                if (!window.zoomSyncGroup.syncCharts.includes(targetChartNum)) {
                                    console.log('[DataZoom] Skipping', targetChartId, '- not in sync group');
                                    continue;
                                }

                                var targetChart = window.zoomSyncGroup.charts[targetChartId];
                                if (targetChart) {
                                    console.log('[DataZoom] Updating', targetChartId, 'to range', start, '-', end);
                                    targetChart.dispatchAction({
                                        type: 'dataZoom',
                                        dataZoomIndex: 0,
                                        start: start,
                                        end: end
                                    });
                                    updatedCount++;
                                }
                            }
                            console.log('[DataZoom] Updated', updatedCount, 'charts');
                        } finally {
                            setTimeout(function() {
                                window.zoomSyncGroup.isUpdating = false;
                                console.log('[DataZoom] Reset isUpdating flag');
                            }, 50);
                        }
                    });

                    resolve();
                } catch (error) {
                    console.error('Chart error:', error);
                    reject(error.message);
                }
            }, function(errorMsg) {
                // Error callback when ECharts fails to load
                reject(errorMsg);
            });
        });
    },

    // Dispose all charts
    disposeAllCharts: function() {
        return new Promise((resolve, reject) => {
            try {
                if (typeof echarts === 'undefined') {
                    resolve(null);
                    return;
                }

                var chartIds = ['chart', 'chart1', 'chart2', 'chart3', 'chart4', 'chart5', 'chart6', 'chart7', 'chart8'];
                var zoomState = null;

                for (var i = 0; i < chartIds.length; i++) {
                    var chartDom = document.getElementById(chartIds[i]);
                    if (chartDom) {
                        var myChart = echarts.getInstanceByDom(chartDom);
                        if (myChart) {
                            if (!zoomState) {
                                var option = myChart.getOption();
                                if (option && option.dataZoom && option.dataZoom.length > 0) {
                                    var start = option.dataZoom[0].start;
                                    var end = option.dataZoom[0].end;
                                    if (start !== undefined && end !== undefined && (start !== 0 || end !== 100)) {
                                        zoomState = [start, end];
                                    }
                                }
                            }
                            myChart.dispose();
                        }
                    }
                }

                resolve(zoomState);
            } catch (error) {
                console.error('Chart disposal error:', error);
                reject(error.message);
            }
        });
    },

    // Resize all charts
    resizeAllCharts: function() {
        return new Promise((resolve, reject) => {
            try {
                if (typeof echarts === 'undefined') {
                    resolve();
                    return;
                }

                var chartIds = ['chart', 'chart1', 'chart2', 'chart3', 'chart4', 'chart5', 'chart6', 'chart7', 'chart8'];
                chartIds.forEach(id => {
                    var chartDom = document.getElementById(id);
                    if (chartDom) {
                        var myChart = echarts.getInstanceByDom(chartDom);
                        if (myChart) {
                            myChart.resize();
                        }
                    }
                });

                resolve();
            } catch (error) {
                console.error('Chart resize error:', error);
                reject(error.message);
            }
        });
    }
};
