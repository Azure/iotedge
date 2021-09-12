use std::{future::Future, time::Duration};

use tokio::{select, sync::mpsc, task::JoinHandle, time::sleep};

pub fn periodic_task_with_interupt<T, Func, Fut>(
    task: Func,
    frequency: Duration,
    mut interrupt: mpsc::Receiver<T>,
    name: &'static str,
) -> JoinHandle<()>
where
    T: Default + Send + Sync + 'static,
    Func: Fn(T) -> Fut + Send + Sync + 'static,
    Fut: Future<Output = ()> + Send + 'static,
{
    println!("Starting task {}", name);
    tokio::spawn(async move {
        println!("Started task {}", name);
        loop {
            select! {
                () = async { sleep(frequency).await; } => {
                    println!("Running Periodic Task {}", name);
                    (task)(Default::default()).await;
                }
                val = async { interrupt.recv().await } => if let Some(val) = val {
                    println!("Interupt Recieved, Running Periodic Task {}", name);
                    (task)(val).await
                } else {
                    println!("Interupt channel closed, ending task {}", name);
                    break;
                },
            }
        }
    })
}
