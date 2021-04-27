using System.Collections.Generic;
using ConstantData.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shared.Library.Models;

namespace ConstantDataTests.Services
{
    [TestClass()]
    public class OnKeysEventsSubscribeServiceTests
    {
        [TestMethod()]
        [DataRow(300, 500, 500)]
        public void UpdatedValueAssignsToPropertyTest(int source, int value, int expected)
        {
            ConstantsSet constantsSet = new ConstantsSet()
            {
                TaskEmulatorDelayTimeInMilliseconds = new ConstantType()
                {
                    Description = "Description",
                    Value = source,
                    LifeTime = 0.99
                },
                FinalPropertyToSet = new KeyType()
                {
                    Description = "ConstantsSet.ConstantType.Value",
                    Value = "Value",
                    LifeTime = 0.999
                }
        };
            IDictionary<string, int> updatedConstants = new Dictionary<string, int>()
            {
                { "TaskEmulatorDelayTimeInMilliseconds", 500 }
                //{ "RecordActualityLevel", 8 }
            };
            //string key = nameof(constantsSet.TaskEmulatorDelayTimeInMilliseconds);

            bool setWasUpdated;
            (setWasUpdated, constantsSet) = OnKeysEventsSubscribeService.UpdatedValueAssignsToProperty(constantsSet, updatedConstants);

            var result = constantsSet.TaskEmulatorDelayTimeInMilliseconds.Value;

            Assert.AreEqual(expected, result);
        }
    }
}