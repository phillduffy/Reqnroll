﻿using Io.Cucumber.Messages.Types;
using Reqnroll.Events;
using System.Collections.Generic;
using System.Linq;

namespace Reqnroll.CucumberMesssages
{
    public class HookStepProcessor : StepProcessorBase
    {
        public string HookBindingSignature { get; private set; }
        public HookBindingFinishedEvent HookBindingFinishedEvent { get; private set; }
        public HookStepProcessor(ScenarioEventProcessor parentScenarioState) : base(parentScenarioState)
        {
        }

        public IEnumerable<Envelope> ProcessEvent(HookBindingStartedEvent stepFinishedEvent)
        {
            TestStepID = parentScenario.IdGenerator.GetNewId();
            HookBindingSignature = CucumberMessageFactory.CanonicalizeHookBinding(stepFinishedEvent.HookBinding);
            return Enumerable.Empty<Envelope>();
        }

        public IEnumerable<Envelope> ProcessEvent(HookBindingFinishedEvent hookFinishedEvent)
        {
            HookBindingFinishedEvent  = hookFinishedEvent;
            Exception = hookFinishedEvent.HookException;
            return Enumerable.Empty<Envelope>();
        }
    }

  
}