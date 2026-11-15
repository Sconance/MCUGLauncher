//! 由领域 JSON Schema 自动生成的 Rust DTO。
//!
//! 每个领域 Schema 都独立经过 Typify 编译，避免前后端契约继续依赖单体文件。

#[allow(clippy::all)]
pub mod generated {
    pub mod accounts {
        typify::import_types!(schema = "../schemas/accounts.schema.json");
    }
    pub mod common {
        typify::import_types!(schema = "../schemas/common.schema.json");
    }
    pub mod content {
        typify::import_types!(schema = "../schemas/content.schema.json");
    }
    pub mod instances {
        typify::import_types!(schema = "../schemas/instances.schema.json");
    }
    pub mod operations {
        typify::import_types!(schema = "../schemas/operations.schema.json");
    }
    pub mod tasks {
        typify::import_types!(schema = "../schemas/tasks.schema.json");
    }
}
