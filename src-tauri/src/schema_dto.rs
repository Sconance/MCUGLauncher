//! 由 JSON Schema 自动生成的 Rust DTO。
//!
//! 业务命令暂时继续使用 `model.rs` 中的行为扩展模型；本模块确保同一份
//! `schemas/launcher.schema.json` 能被 Typify 编译为 Rust 类型。迁移某个 DTO
//! 时，只需把命令返回类型切换到 `generated` 中对应的类型，不需要重复手写字段。

#[allow(clippy::all)]
pub mod generated {
    typify::import_types!(schema = "../schemas/launcher.schema.json");
}
