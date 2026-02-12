using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.VisualBasic;

namespace SKAgent.Agents.Persona
{
    public sealed class PersonaOptions
    {
        public string Name { get; init; } = "default";

        public string SystemPrompt { get; init; } = string.Empty;
        //可选， planner指令(更偏 如何拆计划)
        public string PlannerHint { get; init; }= string.Empty;


    }
}
