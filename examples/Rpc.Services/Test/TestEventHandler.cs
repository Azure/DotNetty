namespace Rpc.Services.Test
{
    using System;
    using DotNetty.Rpc.Service;
    using Rpc.Models;
    using System.Threading.Tasks;
    using Rpc.Models.Test;

    public class TestEventHandler: EventHandlerImpl
    {
        protected override void InitializeComponents()
        {
            this.AddEventListener<TestCityQuery>(this.Handler);
            this.AddEventListener<TestAddressQuery>(this.Handler);
        }

        private Task<TestAddressQuery> Handler(TestAddressQuery eventData)
        {
            throw new NotImplementedException();
        }

        private Task<TestCityQuery> Handler(TestCityQuery eventData)
        {
            eventData.ReturnValue = new CityInfo()
            {
                Id = eventData.Id,
                Name = @"steven_fukua
                        iOS网络层设计感想

                        App的开发无外乎从网络端获取数据显示在屏幕上，数据做些缓存或者持久化，所以网络层极为重要。原来只是把AFNetwork二次封装了一下，使得调用变得很简单，并没有深层次的考虑一些问题。

                        前言

                        参考:
                        网络层设计方案
                        这篇文章提的问题也正是我平时经常纠结的，但是一直没有深入思考。文章给的解决方案和为什么这样做让人茅塞顿开。以下主要就是我的观后感。

                        三个问题

                        使用哪种交互模式来跟业务层做对接？
                        是否有必要将API返回的数据封装成对象然后再交付给业务层？
                        使用集约化调用方式还是离散型调用方式去调用API？
                        我的设计

                        基本上每个网络层都会涉及到这三个问题。
                        我原先的设计是:

                        //APIClient.h
                        @interface APIClient : AFHTTPSessionManager

                        + (instancetype)sharedRequestDataClient;

                        /*
                         * 用json格式(POST)
                         */
                        + (void)requestDataPostMethodWithHTTPPath:(NSString *)path
                                             parameters:(NSDictionary *)parameters
                                                success:(RequestSuccessBlock)success
                                                failure:(RequestFailureBlock)failure;
                        /*
                         * 用json格式(GET)
                         */
                        + (void)requestDataGetMethodWithHTTPPath:(NSString *)path
                                             parameters:(NSDictionary *)parameters
                                                success:(RequestSuccessBlock)success
                                                failure:(RequestFailureBlock)failure;

                        @end

                        //APIManager.h(一个个具体的请求,有多少个请求就有多少个方法)
                        @interface APIManager : NSObject

                        /**
                         *  获取用户信息
                         */
                        + (void)requestUserInfoWithSuccess:(RequestSuccessBlock)success failure:(RequestFailureBlock)failure;

                        ...
                        @end

                        @implementation APIManager

                        /**
                         *  获取用户信息
                         */
                        + (void)requestUserInfoWithSuccess:(RequestSuccessBlock)success
                                                   failure:(RequestFailureBlock)failure { 
                            [APIClient requestDataGetMethodWithHTTPPath:kUserInfo parameters:nil success:success failure:failure];
                        }

                        ...
                        @end
                        APIClient继承AFHTTPSessionManager，里面做了些设置，比方说统一用json,就两个方法，get,post请求。
                        本来还有上传下载数据两个方法，后来所有的资源文件放阿里云上，重建了个APIOSSClient类来处理。
                        APIManager具体处理一个个请求，有多少请求，他就有多少方法。由它来调用APIClient。

                        我的回答

                        数据的传递方式:Block。
                        交付什么样的数据:NSDictionary，在Block回调里把字典处理成最终需要的数据，大多数情况下是model，在model里会有数据处理。
                        APIClient是集约型，很不方便，加了个APIManager，实现离散型的调用，也就是加了个离散型调用的壳。
                        作者的建议

                        数据的传递方式:Delegate。
                        交付什么样的数据:不作处理，添加了reformer（名字而已，叫什么都好)，用于封装数据转化的逻辑。
                        离散型的API调用方式。
                        感想

                        看了作者的思路和源码，发现作者考虑的问题也都想到了，但是处理的方式有很大的问题。

                        传递方式和调用方式

                        最早我接手项目的时候，只有APIClient，简单地做AFNetwork做了封装，属于集约型API调用方式，用block回调是正常的。
                        后来发现集约型API调用方式的弊端太多了，于是我加了个APIManager，规定所有的请求必须在里面加个方法，算是加了个离散调用的壳。但是最后APIManager太大了，好多方法，维护起来好累。
                        所以调用的方式还是离散型的好，因为是离散型的，所有Delegate比Block好。

                        作者一个API对应于一个APIManager，更加容易维护。好处非常多。

                        传递数据

                        对于应该传什么数据，==其实我们的理想情况是希望API的数据下发之后就能够直接被View所展示。首先要说的是，这种情况非常少。另外，这种做法使得View和API联系紧密，也是我们不希望发生的。==
                        这是作者的想法，我们也发现了这个问题，所以会单独在写个类处理，或者转成model，在model里处理，最终变成view需要的数据。如果是model，为了不让view和model耦合，又加了个category传数据。

                        而作者加了个reformer统一处理，并且作者强调去model化，从根源解决了转化成本高，model和view耦合等问题。

                        细节就不讲了，作者开源的网络层很cool，除了使用起来非常方便，功能还非常全，全方面覆盖。小伙伴们自己去学习吧。"
            };
            return Task.FromResult(eventData);
        }
    }
}
