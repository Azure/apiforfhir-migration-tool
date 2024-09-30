param location string = resourceGroup().location
param applicationInsightsName string
param dashboardName string = 'MigrationToolDashboard'
var tags = {
  'app-id': 'fhir-migration-tool'
}

resource applicationInsights 'Microsoft.Insights/components@2020-02-02' existing = {
  name: applicationInsightsName
}

resource migrationToolDashboard 'Microsoft.Portal/dashboards@2020-09-01-preview' = {
  name: dashboardName
  location: location
  tags: tags
  properties: {
    lenses: [
      {
        order: 0
        parts: [
          {
            position: {
              x: 0
              y: 0
              colSpan: 5
              rowSpan: 2
            }
            metadata: {
              inputs: []
              type: 'Extension/HubsExtension/PartType/MarkdownPart'
              settings: {
                content: {
                  content: '## Important reminder: Custom search parameters and $reindex\n\n- The FHIR Migration Tool will copy over custom search parameters from your Azure API for FHIR over to your Azure Health Data Services FHIR service at the very beginning of migration. \n- Once migration has started, if you wish to add any more custom search parameters after that, you must add them directly to the Azure Health Data Service FHIR service post-migration.\n-  Once all your custom search parameters are in Azure Health Data Service FHIR service (regardless of if they were added by the migration tool or manually), you will need to run $reindex post-migration in order to index the custom search parameters and be able to use them in live production.\n- Learn more about custom search parameters:\n[How to do custom search in FHIR service](https://learn.microsoft.com/en-us/azure/healthcare-apis/fhir/how-to-do-custom-search) \nand $reindex:\n[How to run a reindex job in FHIR service](https://learn.microsoft.com/en-us/azure/healthcare-apis/fhir/how-to-run-a-reindex)'
                  title: ''
                  subtitle: ''
                  markdownSource: 1
                  markdownUri: ''
                }
              }
            }
          }
          {
            position: {
              x: 5
              y: 0
              colSpan: 3
              rowSpan: 2
            }
            metadata: {
              inputs: [
                {
                  name: 'resourceTypeMode'
                  isOptional: true
                }
                {
                  name: 'ComponentId'
                  isOptional: true
                }
                {
                  name: 'Scope'
                  value: {
                    resourceIds: [
                      resourceId('Microsoft.Insights/components', applicationInsightsName)
                    ]
                  }
                  isOptional: true
                }
                {
                  name: 'PartId'
                  value: '1098cba3-13b3-47ec-8892-e1246ad15a13'
                  isOptional: true
                }
                {
                  name: 'Version'
                  value: '2.0'
                  isOptional: true
                }
                {
                  name: 'TimeRange'
                  value: 'P7D'
                  isOptional: true
                }
                {
                  name: 'DashboardId'
                  isOptional: true
                }
                {
                  name: 'DraftRequestParameters'
                  isOptional: true
                }
                {
                  name: 'Query'
                  value: 'customEvents\n| where name == "ImportTill"\n| extend till = todatetime(customDimensions.Till)\n| extend Date = format_datetime(till, \'yyyy-MM-dd\'), Time = format_datetime(till, \'HH:mm:ss\')\n| order by till desc\n| take 1\n| distinct Date, Time'
                  isOptional: true
                }
                {
                  name: 'ControlType'
                  value: 'AnalyticsGrid'
                  isOptional: true
                }
                {
                  name: 'SpecificChart'
                  isOptional: true
                }
                {
                  name: 'PartTitle'
                  value: 'Analytics'
                  isOptional: true
                }
                {
                  name: 'PartSubTitle'
                  value: 'TestDurableFunc'
                  isOptional: true
                }
                {
                  name: 'Dimensions'
                  isOptional: true
                }
                {
                  name: 'LegendOptions'
                  isOptional: true
                }
                {
                  name: 'IsQueryContainTimeRange'
                  value: false
                  isOptional: true
                }
              ]
              type: 'Extension/Microsoft_OperationsManagementSuite_Workspace/PartType/LogsDashboardPart'
              settings: {
                content: {
                  Query: 'customEvents\n| where name == "ImportTill"\n| extend till = todatetime(customDimensions.Till)\n| extend Date = format_datetime(till, \'yyyy-MM-dd\'), Time = format_datetime(till, \'HH:mm:ss\')\n| order by till desc\n| take 1\n| distinct Date, Time'
                }
              }
              partHeader: {
                title: 'Migration Status'
                subtitle: 'Data Migrated Till'
              }
            }
          }
          {
            position: {
              x: 0
              y: 2
              colSpan: 4
              rowSpan: 1
            }
            metadata: {
              inputs: []
              type: 'Extension/HubsExtension/PartType/MarkdownPart'
              settings: {
                content: {
                  content: ''
                  title: 'Export Operation Status'
                  subtitle: 'Status of Export Operations'
                  markdownSource: 1
                  markdownUri: ''
                }
              }
              partHeader: {
                title: 'Export Operation Status'
                subtitle: 'Status of Export Operations'
              }
            }
          }
          {
            position: {
              x: 4
              y: 2
              colSpan: 4
              rowSpan: 1
            }
            metadata: {
              inputs: []
              type: 'Extension/HubsExtension/PartType/MarkdownPart'
              settings: {
                content: {
                  content: '\n'
                  title: 'Import Operation Status'
                  subtitle: 'Status of Import Operations'
                  markdownSource: 1
                  markdownUri: ''
                }
              }
              partHeader: {
                title: 'Import Operation Status'
                subtitle: 'Status of Import Operations'
              }
            }
          }
          {
            position: {
              x: 8
              y: 0
              colSpan: 2
              rowSpan: 1
            }
            metadata: {
              inputs: [
                {
                  name: 'id'
                  value: resourceId('Microsoft.Insights/components', applicationInsightsName)
                }
                {
                  name: 'Version'
                  value: '1.0'
                }
              ]
              type: 'Extension/AppInsightsExtension/PartType/AspNetOverviewPinnedPart'
              asset: {
                idInputName: 'id'
                type: 'ApplicationInsights'
              }
              defaultMenuItemId: 'overview'
            }
          }
          {
            position: {
              x: 10
              y: 0
              colSpan: 1
              rowSpan: 1
            }
            metadata: {
              inputs: [
                {
                  name: 'ComponentId'
                  value: {
                    Name: applicationInsightsName
                    SubscriptionId: subscription().subscriptionId
                    ResourceGroup: resourceGroup().name
                  }
                }
                {
                  name: 'Version'
                  value: '1.0'
                }
              ]
              type: 'Extension/AppInsightsExtension/PartType/ProactiveDetectionAsyncPart'
              asset: {
                idInputName: 'ComponentId'
                type: 'ApplicationInsights'
              }
              defaultMenuItemId: 'ProactiveDetection'
            }
          }
          {
            position: {
              x: 11
              y: 0
              colSpan: 1
              rowSpan: 1
            }
            metadata: {
              inputs: [
                {
                  name: 'ComponentId'
                  value: {
                    Name: applicationInsightsName
                    SubscriptionId: subscription().subscriptionId
                    ResourceGroup: resourceGroup().name
                  }
                }
                {
                  name: 'ResourceId'
                  value: resourceId('Microsoft.Insights/components', applicationInsightsName)
                }
              ]
              type: 'Extension/AppInsightsExtension/PartType/QuickPulseButtonSmallPart'
              asset: {
                idInputName: 'ComponentId'
                type: 'ApplicationInsights'
              }
            }
          }
          {
            position: {
              x: 12
              y: 0
              colSpan: 1
              rowSpan: 1
            }
            metadata: {
              inputs: [
                {
                  name: 'ComponentId'
                  value: {
                    Name: applicationInsightsName
                    SubscriptionId: subscription().subscriptionId
                    ResourceGroup: resourceGroup().name
                  }
                }
                {
                  name: 'TimeContext'
                  value: {
                    durationMs: 86400000
                    endTime: null
                    createdTime: '2018-05-04T01:20:33.345Z'
                    isInitialTime: true
                    grain: 1
                    useDashboardTimeRange: false
                  }
                }
                {
                  name: 'Version'
                  value: '1.0'
                }
                {
                  name: 'componentId'
                  isOptional: true
                }
                {
                  name: 'id'
                  isOptional: true
                }
              ]
              type: 'Extension/AppInsightsExtension/PartType/WebTestsPinnedPart'
              asset: {
                idInputName: 'ComponentId'
                type: 'ApplicationInsights'
              }
            }
          }
          {
            position: {
              x: 13
              y: 0
              colSpan: 1
              rowSpan: 1
            }
            metadata: {
              inputs: [
                {
                  name: 'ComponentId'
                  value: {
                    Name: applicationInsightsName
                    SubscriptionId: subscription().subscriptionId
                    ResourceGroup: resourceGroup().name
                  }
                }
                {
                  name: 'TimeContext'
                  value: {
                    durationMs: 86400000
                    endTime: null
                    createdTime: '2018-05-08T18:47:35.237Z'
                    isInitialTime: true
                    grain: 1
                    useDashboardTimeRange: false
                  }
                }
                {
                  name: 'ConfigurationId'
                  value: '78ce933e-e864-4b05-a27b-71fd55a6afad'
                }
                {
                  name: 'MainResourceId'
                  isOptional: true
                }
                {
                  name: 'ResourceIds'
                  isOptional: true
                }
                {
                  name: 'DataModel'
                  isOptional: true
                }
                {
                  name: 'UseCallerTimeContext'
                  isOptional: true
                }
                {
                  name: 'OverrideSettings'
                  isOptional: true
                }
                {
                  name: 'PartId'
                  isOptional: true
                }
              ]
              type: 'Extension/AppInsightsExtension/PartType/ApplicationMapPart'
              settings: {}
              asset: {
                idInputName: 'ComponentId'
                type: 'ApplicationInsights'
              }
            }
          }
          {
            position: {
              x: 0
              y: 3
              colSpan: 4
              rowSpan: 2
            }
            metadata: {
              inputs: [
                {
                  name: 'resourceTypeMode'
                  isOptional: true
                }
                {
                  name: 'ComponentId'
                  isOptional: true
                }
                {
                  name: 'Scope'
                  value: {
                    resourceIds: [
                      resourceId('Microsoft.Insights/components', applicationInsightsName)
                    ]
                  }
                  isOptional: true
                }
                {
                  name: 'PartId'
                  value: '1098cba3-13b3-47ec-8892-e1246ad15a13'
                  isOptional: true
                }
                {
                  name: 'Version'
                  value: '2.0'
                  isOptional: true
                }
                {
                  name: 'TimeRange'
                  value: 'P7D'
                  isOptional: true
                }
                {
                  name: 'DashboardId'
                  isOptional: true
                }
                {
                  name: 'DraftRequestParameters'
                  isOptional: true
                }
                {
                  name: 'Query'
                  value: 'customEvents\n| where name == "Export" and tostring(customDimensions.ExportStatus) == "Completed"\n| distinct  timestamp, tostring(customDimensions.ExportId)\n| order by timestamp desc\n| count\n'
                  isOptional: true
                }
                {
                  name: 'ControlType'
                  value: 'AnalyticsGrid'
                  isOptional: true
                }
                {
                  name: 'SpecificChart'
                  isOptional: true
                }
                {
                  name: 'PartTitle'
                  value: 'Analytics'
                  isOptional: true
                }
                {
                  name: 'PartSubTitle'
                  value: 'TestDurableFunc'
                  isOptional: true
                }
                {
                  name: 'Dimensions'
                  isOptional: true
                }
                {
                  name: 'LegendOptions'
                  isOptional: true
                }
                {
                  name: 'IsQueryContainTimeRange'
                  value: false
                  isOptional: true
                }
              ]
              type: 'Extension/Microsoft_OperationsManagementSuite_Workspace/PartType/LogsDashboardPart'
              settings: {
                content: {
                  Query: 'customEvents\n| where name == "Export" and tostring(customDimensions.ExportStatus) == "Completed"\n| distinct  timestamp, tostring(customDimensions.ExportId)\n| order by timestamp desc\n| count as SuccessfulExports\n\n'
                }
              }
              partHeader: {
                title: 'Completed Exports'
                subtitle: 'Count'
              }
            }
          }
          {
            position: {
              x: 4
              y: 3
              colSpan: 4
              rowSpan: 2
            }
            metadata: {
              inputs: [
                {
                  name: 'resourceTypeMode'
                  isOptional: true
                }
                {
                  name: 'ComponentId'
                  isOptional: true
                }
                {
                  name: 'Scope'
                  value: {
                    resourceIds: [
                      resourceId('Microsoft.Insights/components', applicationInsightsName)
                    ]
                  }
                  isOptional: true
                }
                {
                  name: 'PartId'
                  value: '616c9d10-386f-4043-b713-4046a6768a0c'
                  isOptional: true
                }
                {
                  name: 'Version'
                  value: '2.0'
                  isOptional: true
                }
                {
                  name: 'TimeRange'
                  value: 'P7D'
                  isOptional: true
                }
                {
                  name: 'DashboardId'
                  isOptional: true
                }
                {
                  name: 'DraftRequestParameters'
                  isOptional: true
                }
                {
                  name: 'Query'
                  value: 'customEvents\n| where name == "Import" and tostring(customDimensions.ImportStatus) == "Completed"\n| distinct  timestamp, tostring(customDimensions.ImportId)\n| order by timestamp desc\n| count as ImportSuccess\n'
                  isOptional: true
                }
                {
                  name: 'ControlType'
                  value: 'AnalyticsGrid'
                  isOptional: true
                }
                {
                  name: 'SpecificChart'
                  isOptional: true
                }
                {
                  name: 'PartTitle'
                  value: 'Analytics'
                  isOptional: true
                }
                {
                  name: 'PartSubTitle'
                  value: 'TestDurableFunc'
                  isOptional: true
                }
                {
                  name: 'Dimensions'
                  isOptional: true
                }
                {
                  name: 'LegendOptions'
                  isOptional: true
                }
                {
                  name: 'IsQueryContainTimeRange'
                  value: false
                  isOptional: true
                }
              ]
              type: 'Extension/Microsoft_OperationsManagementSuite_Workspace/PartType/LogsDashboardPart'
              settings: {}
              partHeader: {
                title: 'Completed Imports'
                subtitle: 'count'
              }
            }
          }
          {
            position: {
              x: 8
              y: 1
              colSpan: 3
              rowSpan: 1
            }
            metadata: {
              inputs: []
              type: 'Extension/HubsExtension/PartType/MarkdownPart'
              settings: {
                content: {
                  settings: {
                    content: '# Reliability'
                    title: ''
                    subtitle: ''
                  }
                }
              }
            }
          }
          {
            position: {
              x: 11
              y: 1
              colSpan: 1
              rowSpan: 1
            }
            metadata: {
              inputs: [
                {
                  name: 'ResourceId'
                  value: resourceId('Microsoft.Insights/components', applicationInsightsName)
                }
                {
                  name: 'DataModel'
                  value: {
                    version: '1.0.0'
                    timeContext: {
                      durationMs: 86400000
                      createdTime: '2018-05-04T23:42:40.072Z'
                      isInitialTime: false
                      grain: 1
                      useDashboardTimeRange: false
                    }
                  }
                  isOptional: true
                }
                {
                  name: 'ConfigurationId'
                  value: '8a02f7bf-ac0f-40e1-afe9-f0e72cfee77f'
                  isOptional: true
                }
              ]
              type: 'Extension/AppInsightsExtension/PartType/CuratedBladeFailuresPinnedPart'
              isAdapter: true
              asset: {
                idInputName: 'ResourceId'
                type: 'ApplicationInsights'
              }
              defaultMenuItemId: 'failures'
            }
          }
          {
            position: {
              x: 12
              y: 1
              colSpan: 3
              rowSpan: 1
            }
            metadata: {
              inputs: []
              type: 'Extension/HubsExtension/PartType/MarkdownPart'
              settings: {
                content: {
                  settings: {
                    content: '# Responsiveness\r\n'
                    title: ''
                    subtitle: ''
                  }
                }
              }
            }
          }
          {
            position: {
              x: 15
              y: 1
              colSpan: 1
              rowSpan: 1
            }
            metadata: {
              inputs: [
                {
                  name: 'ResourceId'
                  value: resourceId('Microsoft.Insights/components', applicationInsightsName)
                }
                {
                  name: 'DataModel'
                  value: {
                    version: '1.0.0'
                    timeContext: {
                      durationMs: 86400000
                      createdTime: '2018-05-04T23:43:37.804Z'
                      isInitialTime: false
                      grain: 1
                      useDashboardTimeRange: false
                    }
                  }
                  isOptional: true
                }
                {
                  name: 'ConfigurationId'
                  value: '2a8ede4f-2bee-4b9c-aed9-2db0e8a01865'
                  isOptional: true
                }
              ]
              type: 'Extension/AppInsightsExtension/PartType/CuratedBladePerformancePinnedPart'
              isAdapter: true
              asset: {
                idInputName: 'ResourceId'
                type: 'ApplicationInsights'
              }
              defaultMenuItemId: 'performance'
            }
          }
          {
            position: {
              x: 8
              y: 2
              colSpan: 4
              rowSpan: 3
            }
            metadata: {
              inputs: [
                {
                  name: 'options'
                  value: {
                    chart: {
                      metrics: [
                        {
                          resourceMetadata: {
                            id: resourceId('Microsoft.Insights/components', applicationInsightsName)
                          }
                          name: 'requests/failed'
                          aggregationType: 7
                          namespace: 'microsoft.insights/components'
                          metricVisualization: {
                            displayName: 'Failed requests'
                            color: '#EC008C'
                          }
                        }
                      ]
                      title: 'Failed requests'
                      visualization: {
                        chartType: 3
                        legendVisualization: {
                          isVisible: true
                          position: 2
                          hideSubtitle: false
                        }
                        axisVisualization: {
                          x: {
                            isVisible: true
                            axisType: 2
                          }
                          y: {
                            isVisible: true
                            axisType: 1
                          }
                        }
                      }
                      openBladeOnClick: {
                        openBlade: true
                        destinationBlade: {
                          extensionName: 'HubsExtension'
                          bladeName: 'ResourceMenuBlade'
                          parameters: {
                            id: resourceId('Microsoft.Insights/components', applicationInsightsName)
                            menuid: 'failures'
                          }
                        }
                      }
                    }
                  }
                }
                {
                  name: 'sharedTimeRange'
                  isOptional: true
                }
              ]
              type: 'Extension/HubsExtension/PartType/MonitorChartPart'
              settings: {
                content: {
                  options: {
                    chart: {
                      metrics: [
                        {
                          resourceMetadata: {
                            id: resourceId('Microsoft.Insights/components', applicationInsightsName)
                          }
                          name: 'requests/failed'
                          aggregationType: 7
                          namespace: 'microsoft.insights/components'
                          metricVisualization: {
                            displayName: 'Failed requests'
                            color: '#EC008C'
                          }
                        }
                      ]
                      title: 'Failed requests'
                      visualization: {
                        chartType: 3
                        legendVisualization: {
                          isVisible: true
                          position: 2
                          hideSubtitle: false
                        }
                        axisVisualization: {
                          x: {
                            isVisible: true
                            axisType: 2
                          }
                          y: {
                            isVisible: true
                            axisType: 1
                          }
                        }
                        disablePinning: true
                      }
                      openBladeOnClick: {
                        openBlade: true
                        destinationBlade: {
                          extensionName: 'HubsExtension'
                          bladeName: 'ResourceMenuBlade'
                          parameters: {
                            id: resourceId('Microsoft.Insights/components', applicationInsightsName)
                            menuid: 'failures'
                          }
                        }
                      }
                    }
                  }
                }
              }
            }
          }
          {
            position: {
              x: 12
              y: 2
              colSpan: 4
              rowSpan: 3
            }
            metadata: {
              inputs: [
                {
                  name: 'options'
                  value: {
                    chart: {
                      metrics: [
                        {
                          resourceMetadata: {
                            id: resourceId('Microsoft.Insights/components', applicationInsightsName)
                          }
                          name: 'requests/duration'
                          aggregationType: 4
                          namespace: 'microsoft.insights/components'
                          metricVisualization: {
                            displayName: 'Server response time'
                            color: '#00BCF2'
                          }
                        }
                      ]
                      title: 'Server response time'
                      visualization: {
                        chartType: 2
                        legendVisualization: {
                          isVisible: true
                          position: 2
                          hideSubtitle: false
                        }
                        axisVisualization: {
                          x: {
                            isVisible: true
                            axisType: 2
                          }
                          y: {
                            isVisible: true
                            axisType: 1
                          }
                        }
                      }
                      openBladeOnClick: {
                        openBlade: true
                        destinationBlade: {
                          extensionName: 'HubsExtension'
                          bladeName: 'ResourceMenuBlade'
                          parameters: {
                            id: resourceId('Microsoft.Insights/components', applicationInsightsName)
                            menuid: 'performance'
                          }
                        }
                      }
                    }
                  }
                }
                {
                  name: 'sharedTimeRange'
                  isOptional: true
                }
              ]
              type: 'Extension/HubsExtension/PartType/MonitorChartPart'
              settings: {
                content: {
                  options: {
                    chart: {
                      metrics: [
                        {
                          resourceMetadata: {
                            id: resourceId('Microsoft.Insights/components', applicationInsightsName)
                          }
                          name: 'requests/duration'
                          aggregationType: 4
                          namespace: 'microsoft.insights/components'
                          metricVisualization: {
                            displayName: 'Server response time'
                            color: '#00BCF2'
                          }
                        }
                      ]
                      title: 'Server response time'
                      visualization: {
                        chartType: 2
                        legendVisualization: {
                          isVisible: true
                          position: 2
                          hideSubtitle: false
                        }
                        axisVisualization: {
                          x: {
                            isVisible: true
                            axisType: 2
                          }
                          y: {
                            isVisible: true
                            axisType: 1
                          }
                        }
                        disablePinning: true
                      }
                      openBladeOnClick: {
                        openBlade: true
                        destinationBlade: {
                          extensionName: 'HubsExtension'
                          bladeName: 'ResourceMenuBlade'
                          parameters: {
                            id: resourceId('Microsoft.Insights/components', applicationInsightsName)
                            menuid: 'performance'
                          }
                        }
                      }
                    }
                  }
                }
              }
            }
          }
          {
            position: {
              x: 0
              y: 5
              colSpan: 4
              rowSpan: 2
            }
            metadata: {
              inputs: [
                {
                  name: 'resourceTypeMode'
                  isOptional: true
                }
                {
                  name: 'ComponentId'
                  isOptional: true
                }
                {
                  name: 'Scope'
                  value: {
                    resourceIds: [
                      resourceId('Microsoft.Insights/components', applicationInsightsName)
                    ]
                  }
                  isOptional: true
                }
                {
                  name: 'PartId'
                  value: '1b2b0ee5-12d4-4196-8412-298d4e811702'
                  isOptional: true
                }
                {
                  name: 'Version'
                  value: '2.0'
                  isOptional: true
                }
                {
                  name: 'TimeRange'
                  value: 'P7D'
                  isOptional: true
                }
                {
                  name: 'DashboardId'
                  isOptional: true
                }
                {
                  name: 'DraftRequestParameters'
                  isOptional: true
                }
                {
                  name: 'Query'
                  value: 'let completed = customEvents\n| where name == "Export" and tostring(customDimensions.ExportStatus) == "Completed"\n| distinct  endTime = timestamp, exportId = tostring(customDimensions.ExportId)\n| order by endTime desc;\nlet started = customEvents\n| where name == "Export" and tostring(customDimensions.ExportStatus) == "Started"\n| distinct  startTime = timestamp, exportId = tostring(customDimensions.ExportId)\n| order by startTime desc;\nlet result = completed\n| join kind=innerunique started on exportId;\nresult\n| distinct exportId, startTime, endTime\n'
                  isOptional: true
                }
                {
                  name: 'ControlType'
                  value: 'AnalyticsGrid'
                  isOptional: true
                }
                {
                  name: 'SpecificChart'
                  isOptional: true
                }
                {
                  name: 'PartTitle'
                  value: 'Analytics'
                  isOptional: true
                }
                {
                  name: 'PartSubTitle'
                  value: 'TestDurableFunc'
                  isOptional: true
                }
                {
                  name: 'Dimensions'
                  isOptional: true
                }
                {
                  name: 'LegendOptions'
                  isOptional: true
                }
                {
                  name: 'IsQueryContainTimeRange'
                  value: false
                  isOptional: true
                }
              ]
              type: 'Extension/Microsoft_OperationsManagementSuite_Workspace/PartType/LogsDashboardPart'
              settings: {
                content: {
                  GridColumnsWidth: {
                    exportId: '102.988px'
                    startTime: '104.9884px'
                  }
                  Query: 'let completed = customEvents\n| where name == "Export" and tostring(customDimensions.ExportStatus) == "Completed"\n| distinct  endTime = timestamp, exportId = tostring(customDimensions.ExportId), resources = tostring(customDimensions.TotalResources)\n| order by endTime desc;\nlet started = customEvents\n| where name == "Export" and tostring(customDimensions.ExportStatus) == "Started"\n| distinct  startTime = timestamp, exportId = tostring(customDimensions.ExportId)\n| order by startTime desc;\nlet result = completed\n| join kind=innerunique started on exportId;\nresult\n| distinct exportId, startTime, endTime, resources\n\n'
                }
              }
              partHeader: {
                title: 'Completed Exports'
                subtitle: 'Details'
              }
            }
          }
          {
            position: {
              x: 4
              y: 5
              colSpan: 4
              rowSpan: 2
            }
            metadata: {
              inputs: [
                {
                  name: 'resourceTypeMode'
                  isOptional: true
                }
                {
                  name: 'ComponentId'
                  isOptional: true
                }
                {
                  name: 'Scope'
                  value: {
                    resourceIds: [
                      resourceId('Microsoft.Insights/components', applicationInsightsName)
                    ]
                  }
                  isOptional: true
                }
                {
                  name: 'PartId'
                  value: '153409e2-4ba5-4980-8f8b-36050e1149e2'
                  isOptional: true
                }
                {
                  name: 'Version'
                  value: '2.0'
                  isOptional: true
                }
                {
                  name: 'TimeRange'
                  value: 'P7D'
                  isOptional: true
                }
                {
                  name: 'DashboardId'
                  isOptional: true
                }
                {
                  name: 'DraftRequestParameters'
                  isOptional: true
                }
                {
                  name: 'Query'
                  value: 'let completed = customEvents\n| where name == "Import" and tostring(customDimensions.ImportStatus) == "Completed"\n| distinct  endTime = timestamp, importId = tostring(customDimensions.ImportId)\n| order by endTime desc;\nlet started = customEvents\n| where name == "Import" and tostring(customDimensions.ImportStatus) == "Started"\n| distinct  startTime = timestamp, importId = tostring(customDimensions.ImportId)\n| order by startTime desc;\nlet result = completed\n| join kind=innerunique started on importId;\nresult\n| distinct importId, startTime, endTime\n'
                  isOptional: true
                }
                {
                  name: 'ControlType'
                  value: 'AnalyticsGrid'
                  isOptional: true
                }
                {
                  name: 'SpecificChart'
                  isOptional: true
                }
                {
                  name: 'PartTitle'
                  value: 'Analytics'
                  isOptional: true
                }
                {
                  name: 'PartSubTitle'
                  value: 'TestDurableFunc'
                  isOptional: true
                }
                {
                  name: 'Dimensions'
                  isOptional: true
                }
                {
                  name: 'LegendOptions'
                  isOptional: true
                }
                {
                  name: 'IsQueryContainTimeRange'
                  value: false
                  isOptional: true
                }
              ]
              type: 'Extension/Microsoft_OperationsManagementSuite_Workspace/PartType/LogsDashboardPart'
              settings: {
                content: {
                  Query: 'let completed = customEvents\n| where name == "Import" and tostring(customDimensions.ImportStatus) == "Completed"\n| distinct  endTime = timestamp, importId = tostring(customDimensions.ImportId), resources = tostring(customDimensions.TotalImportResources)\n| order by endTime desc;\nlet started = customEvents\n| where name == "Import" and tostring(customDimensions.ImportStatus) == "Started"\n| distinct  startTime = timestamp, importId = tostring(customDimensions.ImportId)\n| order by startTime desc;\nlet result = completed\n| join kind=innerunique started on importId;\nresult\n| distinct importId, startTime, endTime, resources\n'
                }
              }
              partHeader: {
                title: 'Completed Imports'
                subtitle: 'Details'
              }
            }
          }
          {
            position: {
              x: 0
              y: 7
              colSpan: 4
              rowSpan: 2
            }
            metadata: {
              inputs: [
                {
                  name: 'resourceTypeMode'
                  isOptional: true
                }
                {
                  name: 'ComponentId'
                  isOptional: true
                }
                {
                  name: 'Scope'
                  value: {
                    resourceIds: [
                      resourceId('Microsoft.Insights/components', applicationInsightsName)
                    ]
                  }
                  isOptional: true
                }
                {
                  name: 'PartId'
                  value: '12259543-3da2-4189-849d-f020687cd046'
                  isOptional: true
                }
                {
                  name: 'Version'
                  value: '2.0'
                  isOptional: true
                }
                {
                  name: 'TimeRange'
                  value: 'P7D'
                  isOptional: true
                }
                {
                  name: 'DashboardId'
                  isOptional: true
                }
                {
                  name: 'DraftRequestParameters'
                  isOptional: true
                }
                {
                  name: 'Query'
                  value: 'let completed = customEvents\n| where name == "Export" and tostring(customDimensions.ExportStatus) == "Completed"\n| distinct  timestamp, exportId = tostring(customDimensions.ExportId)\n| order by timestamp desc;\nlet running = customEvents\n| where name == "Export" and tostring(customDimensions.ExportStatus) == "Running"\n| distinct  timestamp, exportId = tostring(customDimensions.ExportId)\n| order by timestamp desc;\nrunning\n| distinct exportId\n| join kind=leftanti completed on exportId\n| count as ExportRunning\n\n'
                  isOptional: true
                }
                {
                  name: 'ControlType'
                  value: 'AnalyticsGrid'
                  isOptional: true
                }
                {
                  name: 'SpecificChart'
                  isOptional: true
                }
                {
                  name: 'PartTitle'
                  value: 'Analytics'
                  isOptional: true
                }
                {
                  name: 'PartSubTitle'
                  value: 'TestDurableFunc'
                  isOptional: true
                }
                {
                  name: 'Dimensions'
                  isOptional: true
                }
                {
                  name: 'LegendOptions'
                  isOptional: true
                }
                {
                  name: 'IsQueryContainTimeRange'
                  value: false
                  isOptional: true
                }
              ]
              type: 'Extension/Microsoft_OperationsManagementSuite_Workspace/PartType/LogsDashboardPart'
              settings: {}
              partHeader: {
                title: 'Running Exports'
                subtitle: 'Count'
              }
            }
          }
          {
            position: {
              x: 4
              y: 7
              colSpan: 4
              rowSpan: 2
            }
            metadata: {
              inputs: [
                {
                  name: 'resourceTypeMode'
                  isOptional: true
                }
                {
                  name: 'ComponentId'
                  isOptional: true
                }
                {
                  name: 'Scope'
                  value: {
                    resourceIds: [
                      resourceId('Microsoft.Insights/components', applicationInsightsName)
                    ]
                  }
                  isOptional: true
                }
                {
                  name: 'PartId'
                  value: '15470d03-113b-4182-b702-b6556d4447cc'
                  isOptional: true
                }
                {
                  name: 'Version'
                  value: '2.0'
                  isOptional: true
                }
                {
                  name: 'TimeRange'
                  value: 'P7D'
                  isOptional: true
                }
                {
                  name: 'DashboardId'
                  isOptional: true
                }
                {
                  name: 'DraftRequestParameters'
                  isOptional: true
                }
                {
                  name: 'Query'
                  value: 'let completed = customEvents\n| where name == "Import" and tostring(customDimensions.ImportStatus) == "Completed"\n| distinct  timestamp, importId = tostring(customDimensions.ImportId)\n| order by timestamp desc;\nlet failed = customEvents\n| where name == "Import" and tostring(customDimensions.ImportStatus) == "Failed"\n| distinct  timestamp, importId = tostring(customDimensions.ImportId)\n| order by timestamp desc;\nlet running = customEvents\n| where name == "Import" and tostring(customDimensions.ImportStatus) == "Running"\n| distinct  timestamp, importId = tostring(customDimensions.ImportId)\n| order by timestamp desc;\nrunning\n| distinct importId\n| join kind=leftanti completed on importId\n| join kind=leftanti failed on importId\n| count as ImportRunning\n'
                  isOptional: true
                }
                {
                  name: 'ControlType'
                  value: 'AnalyticsGrid'
                  isOptional: true
                }
                {
                  name: 'SpecificChart'
                  isOptional: true
                }
                {
                  name: 'PartTitle'
                  value: 'Analytics'
                  isOptional: true
                }
                {
                  name: 'PartSubTitle'
                  value: 'TestDurableFunc'
                  isOptional: true
                }
                {
                  name: 'Dimensions'
                  isOptional: true
                }
                {
                  name: 'LegendOptions'
                  isOptional: true
                }
                {
                  name: 'IsQueryContainTimeRange'
                  value: false
                  isOptional: true
                }
              ]
              type: 'Extension/Microsoft_OperationsManagementSuite_Workspace/PartType/LogsDashboardPart'
              settings: {}
              partHeader: {
                title: 'Running Imports'
                subtitle: 'Count'
              }
            }
          }
          {
            position: {
              x: 8
              y: 5
              colSpan: 4
              rowSpan: 3
            }
            metadata: {
              inputs: [
                {
                  name: 'options'
                  value: {
                    chart: {
                      metrics: [
                        {
                          resourceMetadata: {
                            id: resourceId('Microsoft.Insights/components', applicationInsightsName)
                          }
                          name: 'exceptions/server'
                          aggregationType: 7
                          namespace: 'microsoft.insights/components'
                          metricVisualization: {
                            displayName: 'Server exceptions'
                            color: '#47BDF5'
                          }
                        }
                        {
                          resourceMetadata: {
                            id: resourceId('Microsoft.Insights/components', applicationInsightsName)
                          }
                          name: 'dependencies/failed'
                          aggregationType: 7
                          namespace: 'microsoft.insights/components'
                          metricVisualization: {
                            displayName: 'Dependency failures'
                            color: '#7E58FF'
                          }
                        }
                      ]
                      title: 'Server exceptions and Dependency failures'
                      visualization: {
                        chartType: 2
                        legendVisualization: {
                          isVisible: true
                          position: 2
                          hideSubtitle: false
                        }
                        axisVisualization: {
                          x: {
                            isVisible: true
                            axisType: 2
                          }
                          y: {
                            isVisible: true
                            axisType: 1
                          }
                        }
                      }
                    }
                  }
                }
                {
                  name: 'sharedTimeRange'
                  isOptional: true
                }
              ]
              type: 'Extension/HubsExtension/PartType/MonitorChartPart'
              settings: {
                content: {
                  options: {
                    chart: {
                      metrics: [
                        {
                          resourceMetadata: {
                            id: resourceId('Microsoft.Insights/components', applicationInsightsName)
                          }
                          name: 'exceptions/server'
                          aggregationType: 7
                          namespace: 'microsoft.insights/components'
                          metricVisualization: {
                            displayName: 'Server exceptions'
                            color: '#47BDF5'
                          }
                        }
                        {
                          resourceMetadata: {
                            id: resourceId('Microsoft.Insights/components', applicationInsightsName)
                          }
                          name: 'dependencies/failed'
                          aggregationType: 7
                          namespace: 'microsoft.insights/components'
                          metricVisualization: {
                            displayName: 'Dependency failures'
                            color: '#7E58FF'
                          }
                        }
                      ]
                      title: 'Server exceptions and Dependency failures'
                      visualization: {
                        chartType: 2
                        legendVisualization: {
                          isVisible: true
                          position: 2
                          hideSubtitle: false
                        }
                        axisVisualization: {
                          x: {
                            isVisible: true
                            axisType: 2
                          }
                          y: {
                            isVisible: true
                            axisType: 1
                          }
                        }
                        disablePinning: true
                      }
                    }
                  }
                }
              }
            }
          }
          {
            position: {
              x: 12
              y: 5
              colSpan: 4
              rowSpan: 3
            }
            metadata: {
              inputs: [
                {
                  name: 'options'
                  value: {
                    chart: {
                      metrics: [
                        {
                          resourceMetadata: {
                            id: resourceId('Microsoft.Insights/components', applicationInsightsName)
                          }
                          name: 'performanceCounters/processorCpuPercentage'
                          aggregationType: 4
                          namespace: 'microsoft.insights/components'
                          metricVisualization: {
                            displayName: 'Processor time'
                            color: '#47BDF5'
                          }
                        }
                        {
                          resourceMetadata: {
                            id: resourceId('Microsoft.Insights/components', applicationInsightsName)
                          }
                          name: 'performanceCounters/processCpuPercentage'
                          aggregationType: 4
                          namespace: 'microsoft.insights/components'
                          metricVisualization: {
                            displayName: 'Process CPU'
                            color: '#7E58FF'
                          }
                        }
                      ]
                      title: 'Average processor and process CPU utilization'
                      visualization: {
                        chartType: 2
                        legendVisualization: {
                          isVisible: true
                          position: 2
                          hideSubtitle: false
                        }
                        axisVisualization: {
                          x: {
                            isVisible: true
                            axisType: 2
                          }
                          y: {
                            isVisible: true
                            axisType: 1
                          }
                        }
                      }
                    }
                  }
                }
                {
                  name: 'sharedTimeRange'
                  isOptional: true
                }
              ]
              type: 'Extension/HubsExtension/PartType/MonitorChartPart'
              settings: {
                content: {
                  options: {
                    chart: {
                      metrics: [
                        {
                          resourceMetadata: {
                            id: resourceId('Microsoft.Insights/components', applicationInsightsName)
                          }
                          name: 'performanceCounters/processorCpuPercentage'
                          aggregationType: 4
                          namespace: 'microsoft.insights/components'
                          metricVisualization: {
                            displayName: 'Processor time'
                            color: '#47BDF5'
                          }
                        }
                        {
                          resourceMetadata: {
                            id: resourceId('Microsoft.Insights/components', applicationInsightsName)
                          }
                          name: 'performanceCounters/processCpuPercentage'
                          aggregationType: 4
                          namespace: 'microsoft.insights/components'
                          metricVisualization: {
                            displayName: 'Process CPU'
                            color: '#7E58FF'
                          }
                        }
                      ]
                      title: 'Average processor and process CPU utilization'
                      visualization: {
                        chartType: 2
                        legendVisualization: {
                          isVisible: true
                          position: 2
                          hideSubtitle: false
                        }
                        axisVisualization: {
                          x: {
                            isVisible: true
                            axisType: 2
                          }
                          y: {
                            isVisible: true
                            axisType: 1
                          }
                        }
                        disablePinning: true
                      }
                    }
                  }
                }
              }
            }
          }
          {
            position: {
              x: 0
              y: 9
              colSpan: 4
              rowSpan: 2
            }
            metadata: {
              inputs: [
                {
                  name: 'resourceTypeMode'
                  isOptional: true
                }
                {
                  name: 'ComponentId'
                  isOptional: true
                }
                {
                  name: 'Scope'
                  value: {
                    resourceIds: [
                      resourceId('Microsoft.Insights/components', applicationInsightsName)
                    ]
                  }
                  isOptional: true
                }
                {
                  name: 'PartId'
                  value: '9b5e4e5b-bd38-4cd9-b8d6-32644404378b'
                  isOptional: true
                }
                {
                  name: 'Version'
                  value: '2.0'
                  isOptional: true
                }
                {
                  name: 'TimeRange'
                  value: 'P7D'
                  isOptional: true
                }
                {
                  name: 'DashboardId'
                  isOptional: true
                }
                {
                  name: 'DraftRequestParameters'
                  isOptional: true
                }
                {
                  name: 'Query'
                  value: 'let completed = customEvents\n| where name == "Export" and tostring(customDimensions.ExportStatus) == "Completed"\n| distinct  timestamp, exportId = tostring(customDimensions.ExportId)\n| order by timestamp desc;\nlet failed = customEvents\n| where name == "Export" and tostring(customDimensions.ExportStatus) == "Failed"\n| distinct  timestamp, exportId = tostring(customDimensions.ExportId)\n| order by timestamp desc;\nlet running = customEvents\n| where name == "Export" and tostring(customDimensions.ExportStatus) == "Running"\n| distinct  lastUpdate=timestamp, exportId = tostring(customDimensions.ExportId)\n| order by lastUpdate desc;\nlet started = customEvents\n| where name == "Export" and tostring(customDimensions.ExportStatus) == "Started"\n| distinct  startTime=timestamp, exportId = tostring(customDimensions.ExportId)\n| order by startTime desc;\nlet result = running\n| join kind=leftanti completed on exportId\n| join kind=leftanti failed on exportId\n| join kind=innerunique started on exportId;\nresult\n| distinct exportId, startTime, lastUpdate\n\n'
                  isOptional: true
                }
                {
                  name: 'ControlType'
                  value: 'AnalyticsGrid'
                  isOptional: true
                }
                {
                  name: 'SpecificChart'
                  isOptional: true
                }
                {
                  name: 'PartTitle'
                  value: 'Analytics'
                  isOptional: true
                }
                {
                  name: 'PartSubTitle'
                  value: 'TestDurableFunc'
                  isOptional: true
                }
                {
                  name: 'Dimensions'
                  isOptional: true
                }
                {
                  name: 'LegendOptions'
                  isOptional: true
                }
                {
                  name: 'IsQueryContainTimeRange'
                  value: false
                  isOptional: true
                }
              ]
              type: 'Extension/Microsoft_OperationsManagementSuite_Workspace/PartType/LogsDashboardPart'
              settings: {}
              partHeader: {
                title: 'Running Export Details'
                subtitle: 'Details'
              }
            }
          }
          {
            position: {
              x: 4
              y: 9
              colSpan: 4
              rowSpan: 2
            }
            metadata: {
              inputs: [
                {
                  name: 'resourceTypeMode'
                  isOptional: true
                }
                {
                  name: 'ComponentId'
                  isOptional: true
                }
                {
                  name: 'Scope'
                  value: {
                    resourceIds: [
                      resourceId('Microsoft.Insights/components', applicationInsightsName)
                    ]
                  }
                  isOptional: true
                }
                {
                  name: 'PartId'
                  value: '1cfc5c85-1848-49c4-8e1a-c0ffce147139'
                  isOptional: true
                }
                {
                  name: 'Version'
                  value: '2.0'
                  isOptional: true
                }
                {
                  name: 'TimeRange'
                  value: 'P7D'
                  isOptional: true
                }
                {
                  name: 'DashboardId'
                  isOptional: true
                }
                {
                  name: 'DraftRequestParameters'
                  isOptional: true
                }
                {
                  name: 'Query'
                  value: 'let completed = customEvents\n| where name == "Import" and tostring(customDimensions.ImportStatus) == "Completed"\n| distinct  timestamp, importId = tostring(customDimensions.ImportId)\n| order by timestamp desc;\nlet failed = customEvents\n| where name == "Import" and tostring(customDimensions.ImportStatus) == "Failed"\n| distinct  timestamp, importId = tostring(customDimensions.ImportId)\n| order by timestamp desc;\nlet running = customEvents\n| where name == "Import" and tostring(customDimensions.ImportStatus) == "Running"\n| distinct  lastUpdate=timestamp, importId = tostring(customDimensions.ImportId)\n| order by lastUpdate desc;\nlet started = customEvents\n| where name == "Import" and tostring(customDimensions.ImportStatus) == "Started"\n| distinct  startTime=timestamp, importId = tostring(customDimensions.ImportId)\n| order by startTime desc;\nlet result = running\n| join kind=leftanti completed on importId\n| join kind=leftanti failed on importId\n| join kind=innerunique started on importId;\nresult\n| distinct importId, startTime, lastUpdate\n\n'
                  isOptional: true
                }
                {
                  name: 'ControlType'
                  value: 'AnalyticsGrid'
                  isOptional: true
                }
                {
                  name: 'SpecificChart'
                  isOptional: true
                }
                {
                  name: 'PartTitle'
                  value: 'Analytics'
                  isOptional: true
                }
                {
                  name: 'PartSubTitle'
                  value: 'TestDurableFunc'
                  isOptional: true
                }
                {
                  name: 'Dimensions'
                  isOptional: true
                }
                {
                  name: 'LegendOptions'
                  isOptional: true
                }
                {
                  name: 'IsQueryContainTimeRange'
                  value: false
                  isOptional: true
                }
              ]
              type: 'Extension/Microsoft_OperationsManagementSuite_Workspace/PartType/LogsDashboardPart'
              settings: {}
              partHeader: {
                title: 'Running Imports'
                subtitle: 'Details'
              }
            }
          }
          {
            position: {
              x: 8
              y: 8
              colSpan: 4
              rowSpan: 3
            }
            metadata: {
              inputs: [
                {
                  name: 'options'
                  value: {
                    chart: {
                      metrics: [
                        {
                          resourceMetadata: {
                            id: resourceId('Microsoft.Insights/components', applicationInsightsName)
                          }
                          name: 'performanceCounters/processIOBytesPerSecond'
                          aggregationType: 4
                          namespace: 'microsoft.insights/components'
                          metricVisualization: {
                            displayName: 'Process IO rate'
                            color: '#47BDF5'
                          }
                        }
                      ]
                      title: 'Average process I/O rate'
                      visualization: {
                        chartType: 2
                        legendVisualization: {
                          isVisible: true
                          position: 2
                          hideSubtitle: false
                        }
                        axisVisualization: {
                          x: {
                            isVisible: true
                            axisType: 2
                          }
                          y: {
                            isVisible: true
                            axisType: 1
                          }
                        }
                      }
                    }
                  }
                }
                {
                  name: 'sharedTimeRange'
                  isOptional: true
                }
              ]
              type: 'Extension/HubsExtension/PartType/MonitorChartPart'
              settings: {
                content: {
                  options: {
                    chart: {
                      metrics: [
                        {
                          resourceMetadata: {
                            id: resourceId('Microsoft.Insights/components', applicationInsightsName)
                          }
                          name: 'performanceCounters/processIOBytesPerSecond'
                          aggregationType: 4
                          namespace: 'microsoft.insights/components'
                          metricVisualization: {
                            displayName: 'Process IO rate'
                            color: '#47BDF5'
                          }
                        }
                      ]
                      title: 'Average process I/O rate'
                      visualization: {
                        chartType: 2
                        legendVisualization: {
                          isVisible: true
                          position: 2
                          hideSubtitle: false
                        }
                        axisVisualization: {
                          x: {
                            isVisible: true
                            axisType: 2
                          }
                          y: {
                            isVisible: true
                            axisType: 1
                          }
                        }
                        disablePinning: true
                      }
                    }
                  }
                }
              }
            }
          }
          {
            position: {
              x: 12
              y: 8
              colSpan: 4
              rowSpan: 3
            }
            metadata: {
              inputs: [
                {
                  name: 'options'
                  value: {
                    chart: {
                      metrics: [
                        {
                          resourceMetadata: {
                            id: '/subscriptions/a1766500-6fd5-4f5c-8515-607798271014/resourceGroups/migtool-deploy-rg/providers/Microsoft.Insights/components/miga2xl-appins'
                          }
                          name: 'performanceCounters/memoryAvailableBytes'
                          aggregationType: 4
                          namespace: 'microsoft.insights/components'
                          metricVisualization: {
                            displayName: 'Available memory'
                            color: '#47BDF5'
                          }
                        }
                      ]
                      title: 'Average available memory'
                      visualization: {
                        chartType: 2
                        legendVisualization: {
                          isVisible: true
                          position: 2
                          hideSubtitle: false
                        }
                        axisVisualization: {
                          x: {
                            isVisible: true
                            axisType: 2
                          }
                          y: {
                            isVisible: true
                            axisType: 1
                          }
                        }
                      }
                    }
                  }
                }
                {
                  name: 'sharedTimeRange'
                  isOptional: true
                }
              ]
              type: 'Extension/HubsExtension/PartType/MonitorChartPart'
              settings: {
                content: {
                  options: {
                    chart: {
                      metrics: [
                        {
                          resourceMetadata: {
                            id: '/subscriptions/a1766500-6fd5-4f5c-8515-607798271014/resourceGroups/migtool-deploy-rg/providers/Microsoft.Insights/components/miga2xl-appins'
                          }
                          name: 'performanceCounters/memoryAvailableBytes'
                          aggregationType: 4
                          namespace: 'microsoft.insights/components'
                          metricVisualization: {
                            displayName: 'Available memory'
                            color: '#47BDF5'
                          }
                        }
                      ]
                      title: 'Average available memory'
                      visualization: {
                        chartType: 2
                        legendVisualization: {
                          isVisible: true
                          position: 2
                          hideSubtitle: false
                        }
                        axisVisualization: {
                          x: {
                            isVisible: true
                            axisType: 2
                          }
                          y: {
                            isVisible: true
                            axisType: 1
                          }
                        }
                        disablePinning: true
                      }
                    }
                  }
                }
              }
            }
          }
          {
            position: {
              x: 0
              y: 11
              colSpan: 4
              rowSpan: 2
            }
            metadata: {
              inputs: [
                {
                  name: 'resourceTypeMode'
                  isOptional: true
                }
                {
                  name: 'ComponentId'
                  isOptional: true
                }
                {
                  name: 'Scope'
                  value: {
                    resourceIds: [
                      resourceId('Microsoft.Insights/components', applicationInsightsName)
                    ]
                  }
                  isOptional: true
                }
                {
                  name: 'PartId'
                  value: '00d30db8-81a7-4822-a13b-dc0b7788b925'
                  isOptional: true
                }
                {
                  name: 'Version'
                  value: '2.0'
                  isOptional: true
                }
                {
                  name: 'TimeRange'
                  value: 'P1D'
                  isOptional: true
                }
                {
                  name: 'DashboardId'
                  isOptional: true
                }
                {
                  name: 'DraftRequestParameters'
                  isOptional: true
                }
                {
                  name: 'Query'
                  value: 'customEvents\n| where name == "Export" and tostring(customDimensions.ExportStatus) == "Failed"\n| distinct  timestamp, tostring(customDimensions.ExportId)\n| order by timestamp desc\n| count as ExportFailed\n'
                  isOptional: true
                }
                {
                  name: 'ControlType'
                  value: 'AnalyticsGrid'
                  isOptional: true
                }
                {
                  name: 'SpecificChart'
                  isOptional: true
                }
                {
                  name: 'PartTitle'
                  value: 'Analytics'
                  isOptional: true
                }
                {
                  name: 'PartSubTitle'
                  value: 'TestDurableFunc'
                  isOptional: true
                }
                {
                  name: 'Dimensions'
                  isOptional: true
                }
                {
                  name: 'LegendOptions'
                  isOptional: true
                }
                {
                  name: 'IsQueryContainTimeRange'
                  value: false
                  isOptional: true
                }
              ]
              type: 'Extension/Microsoft_OperationsManagementSuite_Workspace/PartType/LogsDashboardPart'
              settings: {}
              partHeader: {
                title: 'Failed Exports'
                subtitle: 'Count'
              }
            }
          }
          {
            position: {
              x: 4
              y: 11
              colSpan: 4
              rowSpan: 2
            }
            metadata: {
              inputs: [
                {
                  name: 'resourceTypeMode'
                  isOptional: true
                }
                {
                  name: 'ComponentId'
                  isOptional: true
                }
                {
                  name: 'Scope'
                  value: {
                    resourceIds: [
                      resourceId('Microsoft.Insights/components', applicationInsightsName)
                    ]
                  }
                  isOptional: true
                }
                {
                  name: 'PartId'
                  value: '103e3a28-027f-4a5e-9a32-641c70776900'
                  isOptional: true
                }
                {
                  name: 'Version'
                  value: '2.0'
                  isOptional: true
                }
                {
                  name: 'TimeRange'
                  value: 'P7D'
                  isOptional: true
                }
                {
                  name: 'DashboardId'
                  isOptional: true
                }
                {
                  name: 'DraftRequestParameters'
                  isOptional: true
                }
                {
                  name: 'Query'
                  value: 'customEvents\n| where name == "Import" and tostring(customDimensions.ImportStatus) == "Failed"\n| distinct  timestamp, tostring(customDimensions.ImportId)\n| order by timestamp desc\n| count as ExportFailed\n'
                  isOptional: true
                }
                {
                  name: 'ControlType'
                  value: 'AnalyticsGrid'
                  isOptional: true
                }
                {
                  name: 'SpecificChart'
                  isOptional: true
                }
                {
                  name: 'PartTitle'
                  value: 'Analytics'
                  isOptional: true
                }
                {
                  name: 'PartSubTitle'
                  value: 'TestDurableFunc'
                  isOptional: true
                }
                {
                  name: 'Dimensions'
                  isOptional: true
                }
                {
                  name: 'LegendOptions'
                  isOptional: true
                }
                {
                  name: 'IsQueryContainTimeRange'
                  value: false
                  isOptional: true
                }
              ]
              type: 'Extension/Microsoft_OperationsManagementSuite_Workspace/PartType/LogsDashboardPart'
              settings: {
                content: {
                  Query: 'customEvents\n| where name == "Import" and tostring(customDimensions.ImportStatus) == "Failed"\n| distinct  timestamp, tostring(customDimensions.ImportId)\n| order by timestamp desc\n| count as ImportFailed\n\n'
                }
              }
              partHeader: {
                title: 'Failed Imports'
                subtitle: 'Count'
              }
            }
          }
          {
            position: {
              x: 8
              y: 11
              colSpan: 4
              rowSpan: 3
            }
            metadata: {
              inputs: [
                {
                  name: 'options'
                  isOptional: true
                }
                {
                  name: 'sharedTimeRange'
                  isOptional: true
                }
              ]
              type: 'Extension/HubsExtension/PartType/MonitorChartPart'
              settings: {
                content: {
                  options: {
                    chart: {
                      metrics: [
                        {
                          resourceMetadata: {
                            id: resourceId('Microsoft.Insights/components', applicationInsightsName)
                          }
                          name: 'availabilityResults/count'
                          aggregationType: 1
                          namespace: 'microsoft.insights/components/kusto'
                          metricVisualization: {
                            displayName: 'Availability test results count'
                          }
                        }
                      ]
                      title: 'Availability test results count'
                      titleKind: 2
                      visualization: {
                        chartType: 2
                        legendVisualization: {
                          isVisible: true
                          position: 2
                          hideSubtitle: false
                        }
                        axisVisualization: {
                          x: {
                            isVisible: true
                            axisType: 2
                          }
                          y: {
                            isVisible: true
                            axisType: 1
                          }
                        }
                        disablePinning: true
                      }
                    }
                  }
                }
              }
            }
          }
          {
            position: {
              x: 4
              y: 15
              colSpan: 4
              rowSpan: 2
            }
            metadata: {
              inputs: [
                {
                  name: 'resourceTypeMode'
                  isOptional: true
                }
                {
                  name: 'ComponentId'
                  isOptional: true
                }
                {
                  name: 'Scope'
                  value: {
                    resourceIds: [
                      resourceId('Microsoft.Insights/components', applicationInsightsName)
                    ]
                  }
                  isOptional: true
                }
                {
                  name: 'PartId'
                  value: '54cdfb98-4fa8-4e4e-a348-a69c412b4a92'
                  isOptional: true
                }
                {
                  name: 'Version'
                  value: '2.0'
                  isOptional: true
                }
                {
                  name: 'TimeRange'
                  value: 'P7D'
                  isOptional: true
                }
                {
                  name: 'DashboardId'
                  isOptional: true
                }
                {
                  name: 'DraftRequestParameters'
                  isOptional: true
                }
                {
                  name: 'Query'
                  value: 'customEvents\n| where name == "SurfaceCheck"\n| extend Resource = tostring(customDimensions.Resource)\n| extend SourceCount = tostring(customDimensions.SourceCount)\n| extend DestinationCount = tostring(customDimensions.DestinationCount)\n| extend Result= tostring(customDimensions.Result)\n| distinct timestamp,Resource,SourceCount,DestinationCount,Result\n| order by timestamp desc'
                  isOptional: true
                }
                {
                  name: 'ControlType'
                  value: 'AnalyticsGrid'
                  isOptional: true
                }
                {
                  name: 'SpecificChart'
                  isOptional: true
                }
                {
                  name: 'PartTitle'
                  value: 'Analytics'
                  isOptional: true
                }
                {
                  name: 'PartSubTitle'
                  value: 'TestDurableFunc'
                  isOptional: true
                }
                {
                  name: 'Dimensions'
                  isOptional: true
                }
                {
                  name: 'LegendOptions'
                  isOptional: true
                }
                {
                  name: 'IsQueryContainTimeRange'
                  value: false
                  isOptional: true
                }
              ]
              type: 'Extension/Microsoft_OperationsManagementSuite_Workspace/PartType/LogsDashboardPart'
              settings: {}
              partHeader: {
                title: 'Surface Check'
                subtitle: 'Results'
              }
            }
          }
          {
            position: {
              x: 4
              y: 17
              colSpan: 4
              rowSpan: 2
            }
            metadata: {
              inputs: [
                {
                  name: 'resourceTypeMode'
                  isOptional: true
                }
                {
                  name: 'ComponentId'
                  isOptional: true
                }
                {
                  name: 'Scope'
                  value: {
                    resourceIds: [
                      resourceId('Microsoft.Insights/components', applicationInsightsName)
                    ]
                  }
                  isOptional: true
                }
                {
                  name: 'PartId'
                  value: '54cdfb98-4fa8-4e4e-a348-a69c412b4a92'
                  isOptional: true
                }
                {
                  name: 'Version'
                  value: '2.0'
                  isOptional: true
                }
                {
                  name: 'TimeRange'
                  value: 'P7D'
                  isOptional: true
                }
                {
                  name: 'DashboardId'
                  isOptional: true
                }
                {
                  name: 'DraftRequestParameters'
                  isOptional: true
                }
                {
                  name: 'Query'
                  value: 'customEvents\n| where name == "DeepCheck"\n| extend Resource = tostring(customDimensions.Resource)\n| extend ResourceId = tostring(customDimensions.id)\n| extend Result= tostring(customDimensions.Result)\n| distinct timestamp,Resource,ResourceId,Result\n| order by timestamp desc\n'
                  isOptional: true
                }
                {
                  name: 'ControlType'
                  value: 'AnalyticsGrid'
                  isOptional: true
                }
                {
                  name: 'SpecificChart'
                  isOptional: true
                }
                {
                  name: 'PartTitle'
                  value: 'Analytics'
                  isOptional: true
                }
                {
                  name: 'PartSubTitle'
                  value: 'TestDurableFunc'
                  isOptional: true
                }
                {
                  name: 'Dimensions'
                  isOptional: true
                }
                {
                  name: 'LegendOptions'
                  isOptional: true
                }
                {
                  name: 'IsQueryContainTimeRange'
                  value: false
                  isOptional: true
                }
              ]
              type: 'Extension/Microsoft_OperationsManagementSuite_Workspace/PartType/LogsDashboardPart'
              settings: {}
              partHeader: {
                title: 'Deep Check'
                subtitle: 'Results'
              }
            }
          }
          {
            position: {
              x: 0
              y: 13
              colSpan: 4
              rowSpan: 2
            }
            metadata: {
              inputs: [
                {
                  name: 'resourceTypeMode'
                  isOptional: true
                }
                {
                  name: 'ComponentId'
                  isOptional: true
                }
                {
                  name: 'Scope'
                  value: {
                    resourceIds: [
                      resourceId('Microsoft.Insights/components', applicationInsightsName)
                    ]
                  }
                  isOptional: true
                }
                {
                  name: 'PartId'
                  value: '15214c4e-15eb-4021-af58-3b9b3da15352'
                  isOptional: true
                }
                {
                  name: 'Version'
                  value: '2.0'
                  isOptional: true
                }
                {
                  name: 'TimeRange'
                  value: 'P7D'
                  isOptional: true
                }
                {
                  name: 'DashboardId'
                  isOptional: true
                }
                {
                  name: 'DraftRequestParameters'
                  isOptional: true
                }
                {
                  name: 'Query'
                  value: 'let completed = customEvents\n| where name == "Export" and tostring(customDimensions.ExportStatus) == "Failed"\n| distinct  endTime = timestamp, exportId = tostring(customDimensions.ExportId), reason = tostring(customDimensions.FailureReason)\n| order by endTime desc;\nlet started = customEvents\n| where name == "Export" and tostring(customDimensions.ExportStatus) == "Started"\n| distinct  startTime = timestamp, exportId = tostring(customDimensions.ExportId)\n| order by startTime desc;\nlet result = completed\n| join kind=innerunique started on exportId;\nresult\n| distinct exportId,reason, startTime, endTime\n\n'
                  isOptional: true
                }
                {
                  name: 'ControlType'
                  value: 'AnalyticsGrid'
                  isOptional: true
                }
                {
                  name: 'SpecificChart'
                  isOptional: true
                }
                {
                  name: 'PartTitle'
                  value: 'Analytics'
                  isOptional: true
                }
                {
                  name: 'PartSubTitle'
                  value: 'TestDurableFunc'
                  isOptional: true
                }
                {
                  name: 'Dimensions'
                  isOptional: true
                }
                {
                  name: 'LegendOptions'
                  isOptional: true
                }
                {
                  name: 'IsQueryContainTimeRange'
                  value: false
                  isOptional: true
                }
              ]
              type: 'Extension/Microsoft_OperationsManagementSuite_Workspace/PartType/LogsDashboardPart'
              settings: {}
              partHeader: {
                title: 'Failed Exports'
                subtitle: 'Details'
              }
            }
          }
          {
            position: {
              x: 4
              y: 13
              colSpan: 4
              rowSpan: 2
            }
            metadata: {
              inputs: [
                {
                  name: 'resourceTypeMode'
                  isOptional: true
                }
                {
                  name: 'ComponentId'
                  isOptional: true
                }
                {
                  name: 'Scope'
                  value: {
                    resourceIds: [
                      resourceId('Microsoft.Insights/components', applicationInsightsName)
                    ]
                  }
                  isOptional: true
                }
                {
                  name: 'PartId'
                  value: '29a7fde8-5b4d-4337-a212-46dedf81383c'
                  isOptional: true
                }
                {
                  name: 'Version'
                  value: '2.0'
                  isOptional: true
                }
                {
                  name: 'TimeRange'
                  value: 'P7D'
                  isOptional: true
                }
                {
                  name: 'DashboardId'
                  isOptional: true
                }
                {
                  name: 'DraftRequestParameters'
                  isOptional: true
                }
                {
                  name: 'Query'
                  value: 'let failed = customEvents\n| where name == "Import" and tostring(customDimensions.ImportStatus) == "Failed"\n| distinct  endTime = timestamp, importId = tostring(customDimensions.ImportId), reason = tostring(customDimensions.FailureReason)\n| order by endTime desc;\nlet started = customEvents\n| where name == "Import" and tostring(customDimensions.ImportStatus) == "Started"\n| distinct  startTime = timestamp, importId = tostring(customDimensions.ImportId)\n| order by startTime desc;\nlet result = failed\n| join kind=innerunique started on importId;\nresult\n| distinct importId,reason, startTime, endTime\n\n'
                  isOptional: true
                }
                {
                  name: 'ControlType'
                  value: 'AnalyticsGrid'
                  isOptional: true
                }
                {
                  name: 'SpecificChart'
                  isOptional: true
                }
                {
                  name: 'PartTitle'
                  value: 'Analytics'
                  isOptional: true
                }
                {
                  name: 'PartSubTitle'
                  value: 'TestDurableFunc'
                  isOptional: true
                }
                {
                  name: 'Dimensions'
                  isOptional: true
                }
                {
                  name: 'LegendOptions'
                  isOptional: true
                }
                {
                  name: 'IsQueryContainTimeRange'
                  value: false
                  isOptional: true
                }
              ]
              type: 'Extension/Microsoft_OperationsManagementSuite_Workspace/PartType/LogsDashboardPart'
              settings: {}
              partHeader: {
                title: 'Failed Import'
                subtitle: 'Details'
              }
            }
          }
          {
            position: {
              x: 0
              y: 15
              colSpan: 4
              rowSpan: 2
            }
            metadata: {
              inputs: [
                {
                  name: 'resourceTypeMode'
                  isOptional: true
                }
                {
                  name: 'ComponentId'
                  isOptional: true
                }
                {
                  name: 'Scope'
                  value: {
                    resourceIds: [
                      resourceId('Microsoft.Insights/components', applicationInsightsName)
                    ]
                  }
                  isOptional: true
                }
                {
                  name: 'PartId'
                  value: '103e3a28-027f-4a5e-9a32-641c70776900'
                  isOptional: true
                }
                {
                  name: 'Version'
                  value: '2.0'
                  isOptional: true
                }
                {
                  name: 'TimeRange'
                  value: 'P7D'
                  isOptional: true
                }
                {
                  name: 'DashboardId'
                  isOptional: true
                }
                {
                  name: 'DraftRequestParameters'
                  isOptional: true
                }
                {
                  name: 'Query'
                  value: 'customEvents\n| where name == "Import" and tostring(customDimensions.ImportStatus) == "Failed"\n| distinct  timestamp, tostring(customDimensions.ImportId)\n| order by timestamp desc\n| count as ExportFailed\n'
                  isOptional: true
                }
                {
                  name: 'ControlType'
                  value: 'AnalyticsGrid'
                  isOptional: true
                }
                {
                  name: 'SpecificChart'
                  isOptional: true
                }
                {
                  name: 'PartTitle'
                  value: 'Analytics'
                  isOptional: true
                }
                {
                  name: 'PartSubTitle'
                  value: 'TestDurableFunc'
                  isOptional: true
                }
                {
                  name: 'Dimensions'
                  isOptional: true
                }
                {
                  name: 'LegendOptions'
                  isOptional: true
                }
                {
                  name: 'IsQueryContainTimeRange'
                  value: false
                  isOptional: true
                }
              ]
              type: 'Extension/Microsoft_OperationsManagementSuite_Workspace/PartType/LogsDashboardPart'
              settings: {
                content: {
                  Query: 'customEvents \n| where name == "Import" and tostring(customDimensions.ImportStatus) == "Completed"\n| extend SourceResourceCount = tostring(customDimensions.SourceResourceCount)\n| extend DestinationResourceCount = tostring(customDimensions.DestinationResourceCount)\n| distinct timestamp, SourceResourceCount, DestinationResourceCount \n| order by timestamp desc\n| take 1'
                }
              }
              partHeader: {
                title: 'Total Resource Count'
                subtitle: 'Count'
              }
            }
          }
          {
            position: {
              x: 0
              y: 17
              colSpan: 4
              rowSpan: 2
            }
            metadata: {
              inputs: [
                {
                  name: 'resourceTypeMode'
                  isOptional: true
                }
                {
                  name: 'ComponentId'
                  isOptional: true
                }
                {
                  name: 'Scope'
                  value: {
                    resourceIds: [
                      resourceId('Microsoft.Insights/components', applicationInsightsName)
                    ]
                  }
                  isOptional: true
                }
                {
                  name: 'PartId'
                  value: '103e3a28-027f-4a5e-9a32-641c70776900'
                  isOptional: true
                }
                {
                  name: 'Version'
                  value: '2.0'
                  isOptional: true
                }
                {
                  name: 'TimeRange'
                  value: 'P7D'
                  isOptional: true
                }
                {
                  name: 'DashboardId'
                  isOptional: true
                }
                {
                  name: 'DraftRequestParameters'
                  isOptional: true
                }
                {
                  name: 'Query'
                  value: 'customEvents\n| where name == "Import" and tostring(customDimensions.ImportStatus) == "Failed"\n| distinct  timestamp, tostring(customDimensions.ImportId)\n| order by timestamp desc\n| count as ExportFailed\n'
                  isOptional: true
                }
                {
                  name: 'ControlType'
                  value: 'AnalyticsGrid'
                  isOptional: true
                }
                {
                  name: 'SpecificChart'
                  isOptional: true
                }
                {
                  name: 'PartTitle'
                  value: 'Analytics'
                  isOptional: true
                }
                {
                  name: 'PartSubTitle'
                  value: 'TestDurableFunc'
                  isOptional: true
                }
                {
                  name: 'Dimensions'
                  isOptional: true
                }
                {
                  name: 'LegendOptions'
                  isOptional: true
                }
                {
                  name: 'IsQueryContainTimeRange'
                  value: false
                  isOptional: true
                }
              ]
              type: 'Extension/Microsoft_OperationsManagementSuite_Workspace/PartType/LogsDashboardPart'
              settings: {
                content: {
                  Query: 'customEvents | where name == "Import" and tostring(customDimensions.ImportStatus) == "Completed"\n| extend TotalExpoertedResources = tostring(customDimensions.TotalExportResources)\n| extend TotalImportResources = tostring(customDimensions.TotalImportResources)\n| distinct timestamp, TotalExpoertedResources, TotalImportResources\n| order by timestamp desc  \n'
                }
              }
              partHeader: {
                title: 'Export and Import Resources'
                subtitle: 'Details'
              }
            }
          }
        ]
      }
    ]
  }
}
