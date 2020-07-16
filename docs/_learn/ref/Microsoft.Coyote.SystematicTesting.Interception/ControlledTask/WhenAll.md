---
layout: reference
section: learn
title: WhenAll
permalink: /learn/ref/Microsoft.Coyote.SystematicTesting.Interception/ControlledTask/WhenAll
---
# ControlledTask.WhenAll method (1 of 4)

Creates a Task that will complete when all tasks in the specified enumerable collection have completed.

```csharp
public static Task WhenAll(IEnumerable<Task> tasks)
```

| parameter | description |
| --- | --- |
| tasks | The tasks to wait for completion. |

## Return Value

Task that represents the completion of all of the specified tasks.

## See Also

* class [ControlledTask](../ControlledTaskType)
* namespace [Microsoft.Coyote.SystematicTesting.Interception](../ControlledTaskType)
* assembly [Microsoft.Coyote](../../MicrosoftCoyoteAssembly)

---

# ControlledTask.WhenAll method (2 of 4)

Creates a Task that will complete when all tasks in the specified array have completed.

```csharp
public static Task WhenAll(params Task[] tasks)
```

| parameter | description |
| --- | --- |
| tasks | The tasks to wait for completion. |

## Return Value

Task that represents the completion of all of the specified tasks.

## See Also

* class [ControlledTask](../ControlledTaskType)
* namespace [Microsoft.Coyote.SystematicTesting.Interception](../ControlledTaskType)
* assembly [Microsoft.Coyote](../../MicrosoftCoyoteAssembly)

---

# ControlledTask.WhenAll&lt;TResult&gt; method (3 of 4)

Creates a Task that will complete when all tasks in the specified enumerable collection have completed.

```csharp
public static Task<TResult[]> WhenAll<TResult>(IEnumerable<Task<TResult>> tasks)
```

| parameter | description |
| --- | --- |
| TResult | The result type of the task. |
| tasks | The tasks to wait for completion. |

## Return Value

Task that represents the completion of all of the specified tasks.

## See Also

* class [ControlledTask](../ControlledTaskType)
* namespace [Microsoft.Coyote.SystematicTesting.Interception](../ControlledTaskType)
* assembly [Microsoft.Coyote](../../MicrosoftCoyoteAssembly)

---

# ControlledTask.WhenAll&lt;TResult&gt; method (4 of 4)

Creates a Task that will complete when all tasks in the specified array have completed.

```csharp
public static Task<TResult[]> WhenAll<TResult>(params Task<TResult>[] tasks)
```

| parameter | description |
| --- | --- |
| TResult | The result type of the task. |
| tasks | The tasks to wait for completion. |

## Return Value

Task that represents the completion of all of the specified tasks.

## See Also

* class [ControlledTask](../ControlledTaskType)
* namespace [Microsoft.Coyote.SystematicTesting.Interception](../ControlledTaskType)
* assembly [Microsoft.Coyote](../../MicrosoftCoyoteAssembly)

<!-- DO NOT EDIT: generated by xmldocmd for Microsoft.Coyote.dll -->