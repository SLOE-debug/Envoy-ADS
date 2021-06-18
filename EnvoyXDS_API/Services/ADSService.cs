using Envoy.Config.Cluster.V3;
using Envoy.Config.Endpoint.V3;
using Envoy.Config.Listener.V3;
using Envoy.Config.Route.V3;
using Envoy.Extensions.Filters.Network.HttpConnectionManager.V3;
using Envoy.Service.Discovery.V3;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.AspNetCore.Components;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace EnvoyXDS_API.Services
{
    public class ADSService : AggregatedDiscoveryService.AggregatedDiscoveryServiceBase
    {
        public override async Task StreamAggregatedResources(IAsyncStreamReader<Envoy.Service.Discovery.V3.DiscoveryRequest> requestStream, IServerStreamWriter<Envoy.Service.Discovery.V3.DiscoveryResponse> responseStream, ServerCallContext context)
        {
            await foreach (var message in requestStream.ReadAllAsync(context.CancellationToken))
            {
                if (message.VersionInfo == "1.0") continue;
                switch (message.TypeUrl)
                {
                    case "type.googleapis.com/envoy.config.listener.v3.Listener":
                        await responseStream.WriteAsync(GetListener(message.TypeUrl));
                        break;
                    case "type.googleapis.com/envoy.config.cluster.v3.Cluster":
                        await responseStream.WriteAsync(GetCluster(message.TypeUrl));
                        break;
                    default:
                        break;
                }
                if (message.TypeUrl != "type.googleapis.com/envoy.config.listener.v3.Listener" || message.VersionInfo == "1.0") continue;
            }
        }


        public DiscoveryResponse GetCluster(string Type_Url)
        {
            DiscoveryResponse res = new DiscoveryResponse();
            Cluster c1 = new Cluster() { Name = "local_cluster", ConnectTimeout = new Duration { Seconds = 30 }, Type = Cluster.Types.DiscoveryType.Static, DnsLookupFamily = Cluster.Types.DnsLookupFamily.V4Only };
            //c1.LbPolicy = Cluster.Types.LbPolicy.RingHash;
            ClusterLoadAssignment loadass = new ClusterLoadAssignment { ClusterName = "local_cluster" };
            LocalityLbEndpoints local_lbEndpoints = new LocalityLbEndpoints();
            local_lbEndpoints.LbEndpoints.Add(new LbEndpoint
            {
                Endpoint = new Endpoint
                {
                    Address = new Envoy.Config.Core.V3.Address
                    {
                        SocketAddress = new Envoy.Config.Core.V3.SocketAddress
                        {
                            Address = "192.168.190.1",
                            PortValue = 6650
                        }
                    }
                },
            });
            local_lbEndpoints.LbEndpoints.Add(new LbEndpoint
            {
                Endpoint = new Endpoint
                {
                    Address = new Envoy.Config.Core.V3.Address
                    {
                        SocketAddress = new Envoy.Config.Core.V3.SocketAddress
                        {
                            Address = "192.168.190.1",
                            PortValue = 6660
                        }
                    }
                },
            });
            loadass.Endpoints.Add(local_lbEndpoints);
            Envoy.Config.Core.V3.HealthCheck hcheck1 = new Envoy.Config.Core.V3.HealthCheck()
            {
                Timeout = new Duration
                {
                    Seconds = 30
                },
                Interval = new Duration
                {
                    Seconds = 20
                },
                UnhealthyThreshold = 2,
                HealthyThreshold = 1,
            };
            hcheck1.HttpHealthCheck = new Envoy.Config.Core.V3.HealthCheck.Types.HttpHealthCheck { Host = "local_cluster", Path = "/healthcheck", CodecClientType = Envoy.Type.V3.CodecClientType.Http1 };
            c1.HealthChecks.Add(hcheck1);
            c1.LoadAssignment = loadass;
            //c1.CommonLbConfig = new Cluster.Types.CommonLbConfig { HealthyPanicThreshold = new Envoy.Type.V3.Percent { Value = 0.0 } };

            res.VersionInfo = "1.0";
            res.TypeUrl = Type_Url;
            res.Resources.Add(Any.Pack(c1));
            return res;
        }

        public DiscoveryResponse GetListener(string Type_Url)
        {
            DiscoveryResponse res = new DiscoveryResponse();
            var l1 = new Listener() { Name = "5566" };
            l1.Address = new Envoy.Config.Core.V3.Address { SocketAddress = new Envoy.Config.Core.V3.SocketAddress { Address = "0.0.0.0", PortValue = 5566 } };
            var filterChain = new FilterChain();
            var httpmangaer = new HttpConnectionManager() { StatPrefix = "ingress_http" };
            httpmangaer.HttpFilters.Add(new HttpFilter { Name = "envoy.filters.http.router" });
            httpmangaer.RouteConfig = new RouteConfiguration { Name = "local_route" };

            var virtual_host = new VirtualHost() { Name = "local_service" };
            virtual_host.Domains.Add("*");

            var route = new Route();
            route.Match = new RouteMatch { Prefix = "/" };
            route.Route_ = new RouteAction { Cluster = "local_cluster" };

            virtual_host.Routes.Add(route);

            httpmangaer.RouteConfig.VirtualHosts.Add(virtual_host);

            filterChain.Filters.Add(new Envoy.Config.Listener.V3.Filter { Name = "envoy.http_connection_manager", TypedConfig = Any.Pack(httpmangaer) });

            l1.FilterChains.Add(filterChain);

            res.VersionInfo = "1.0";
            res.TypeUrl = Type_Url;
            res.Resources.Add(Any.Pack(l1));
            return res;
        }
    }
}
