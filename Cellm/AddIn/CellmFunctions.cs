﻿using System.Text;
using Cellm.AddIn.Exceptions;
using Cellm.AddIn.Prompts;
using Cellm.Models;
using Cellm.Services;
using ExcelDna.Integration;
using Microsoft.Extensions.Options;

namespace Cellm.AddIn;

public static class CellmFunctions
{
    [ExcelFunction(Name = "PROMPT", Description = "Send a prompt to the default model")]
    public static object Prompt(
    [ExcelArgument(AllowReference = true, Name = "Context", Description = "A cell or range of cells")] object context,
    [ExcelArgument(Name = "InstructionsOrTemperature", Description = "A cell or range of cells with instructions or a temperature")] object instructionsOrTemperature,
    [ExcelArgument(Name = "Temperature", Description = "Temperature")] object temperature)
    {
        var cellmConfiguration = ServiceLocator.Get<IOptions<CellmConfiguration>>().Value;

        return PromptModel(
            $"{cellmConfiguration.DefaultModelProvider}/{cellmConfiguration.DefaultModel}",
            context, 
            instructionsOrTemperature, 
            temperature);
    }

    [ExcelFunction(Name = "PROMPTMODEL", Description = "Send a prompt to a specific model")]
    public static object PromptModel(
        [ExcelArgument(AllowReference = true, Name = "Provider/Model")] object providerAndModel,
        [ExcelArgument(AllowReference = true, Name = "Context", Description = "A cell or range of cells")] object context,
        [ExcelArgument(Name = "InstructionsOrTemperature", Description = "A cell or range of cells with instructions or a temperature")] object instructionsOrTemperature,
        [ExcelArgument(Name = "Temperature", Description = "Temperature")] object temperature)
    {
        try
        {
            var arguments = ServiceLocator.Get<ArgumentParser>()
                .AddProvider(providerAndModel)
                .AddModel(providerAndModel)
                .AddContext(context)
                .AddInstructionsOrTemperature(instructionsOrTemperature)
                .AddTemperature(temperature)
                .Parse();

            var userMessage = new StringBuilder()
                .AppendLine(arguments.Instructions)
                .AppendLine(arguments.Context)
                .ToString();

            var prompt = new PromptBuilder()
                .SetSystemMessage(CellmPrompts.SystemMessage)
                .SetTemperature(arguments.Temperature)
                .AddUserMessage(userMessage)
                .Build();

            // ExcelAsyncUtil yields Excel's main thread, Task.Run enables async/await in inner code
            return ExcelAsyncUtil.Run(nameof(Prompt), new object[] { context, instructionsOrTemperature, temperature }, () =>
            {
                return Task.Run(async () => await CallModelAsync(prompt)).GetAwaiter().GetResult();
            });
        }
        catch (CellmException ex)
        {
            SentrySdk.CaptureException(ex);
            return ex.Message;
        }
    }

    private static async Task<string> CallModelAsync(Prompt prompt, string? provider = null, string? model = null)
    {
        try
        {
            var client = ServiceLocator.Get<IClient>();
            var response = await client.Send(prompt, provider, model);
            return response.Messages.Last().Content;
        }
        catch (CellmException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new CellmException("An unexpected error occurred", ex);
        }
    }
}
