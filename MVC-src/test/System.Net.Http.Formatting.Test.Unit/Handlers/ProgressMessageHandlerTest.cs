﻿// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Extensions;

namespace System.Net.Http.Handlers
{
    public class ProgressMessageHandlerTest
    {
        private const string TestHeader = "TestHeader";
        private const string TestValue = "TestValue";

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public Task SendAsync_DoesNotInsertSendProgressWithoutEntityOrHandlerPresent(bool insertRequestEntity, bool addSendProgressHandler)
        {
            // Arrange
            HttpMessageInvoker invoker = CreateMessageInvoker(includeResponseEntity: false, addReceiveProgressHandler: false, addSendProgressHandler: addSendProgressHandler);
            HttpRequestMessage request = new HttpRequestMessage();
            HttpContent content = null;
            if (insertRequestEntity)
            {
                content = new StringContent("Request Entity!");
                content.Headers.Add(TestHeader, TestValue);
                request.Content = content;
            }

            // Act
            return invoker.SendAsync(request, CancellationToken.None).ContinueWith(
                task =>
                {
                    // Assert
                    Assert.Equal(TaskStatus.RanToCompletion, task.Status);
                    if (insertRequestEntity && addSendProgressHandler)
                    {
                        ValidateContentHeader(request.Content);
                        Assert.NotSame(content, request.Content);
                        Assert.IsType<ProgressContent>(request.Content);
                    }
                    else
                    {
                        if (insertRequestEntity)
                        {
                            Assert.IsType<StringContent>(request.Content);
                        }
                        else
                        {
                            Assert.Null(request.Content);
                        }
                    }
                });
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public Task SendAsync_InsertsReceiveProgressWhenResponseEntityPresent(bool insertResponseEntity, bool addReceiveProgressHandler)
        {
            // Arrange
            HttpMessageInvoker invoker = CreateMessageInvoker(includeResponseEntity: insertResponseEntity, addSendProgressHandler: false, addReceiveProgressHandler: addReceiveProgressHandler);
            HttpRequestMessage request = new HttpRequestMessage();

            // Act
            return invoker.SendAsync(request, CancellationToken.None).ContinueWith(
                task =>
                {
                    HttpResponseMessage response = task.Result;
                    Assert.Equal(TaskStatus.RanToCompletion, task.Status);

                    // Assert
                    if (insertResponseEntity && addReceiveProgressHandler)
                    {
                        ValidateContentHeader(response.Content);
                        Assert.Equal(TaskStatus.RanToCompletion, task.Status);
                        Assert.NotNull(response.Content);
                        Assert.IsType<StreamContent>(response.Content);
                    }
                    else
                    {
                        if (insertResponseEntity)
                        {
                            Assert.IsType<StringContent>(response.Content);
                        }
                        else
                        {
                            Assert.Null(response.Content);
                        }
                    }
                });
        }

        private static HttpMessageInvoker CreateMessageInvoker(bool includeResponseEntity, bool addSendProgressHandler, bool addReceiveProgressHandler)
        {
            ShortCircuitMessageHandler innerHandler = new ShortCircuitMessageHandler(includeResponseEntity);
            ProgressMessageHandler progress = new ProgressMessageHandler(innerHandler);
            if (addSendProgressHandler)
            {
                progress.HttpSendProgress += new MockProgressEventHandler().Handler;
            }

            if (addReceiveProgressHandler)
            {
                progress.HttpReceiveProgress += new MockProgressEventHandler().Handler;
            }

            return new HttpMessageInvoker(progress);
        }

        private static void ValidateContentHeader(HttpContent content)
        {
            IEnumerable<string> values;
            bool headerResult = content.Headers.TryGetValues(TestHeader, out values);
            Assert.True(headerResult);
            Assert.Equal(TestValue, values.First());
        }

        private class ShortCircuitMessageHandler : HttpMessageHandler
        {
            bool _includeResponseEntity;

            public ShortCircuitMessageHandler(bool includeResponseEntity)
            {
                _includeResponseEntity = includeResponseEntity;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                HttpResponseMessage response = request.CreateResponse();
                if (_includeResponseEntity)
                {
                    response.Content = new StringContent("Response Entity");
                    response.Content.Headers.Add(TestHeader, TestValue);
                }
                return TaskHelpers.FromResult(response);
            }
        }
    }
}
