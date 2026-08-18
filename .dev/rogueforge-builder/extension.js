// RogueForge Builder - 在资源管理器右键任意文件，选择 "RogueForge: 一键构建并部署"
const vscode = require('vscode');
const fs = require('fs');
const path = require('path');

function findScript() {
    const folders = vscode.workspace.workspaceFolders || [];
    for (const folder of folders) {
        const candidate = path.join(folder.uri.fsPath, 'BuildAndDeploy.ps1');
        if (fs.existsSync(candidate)) return candidate;
    }
    return null;
}

function activate(context) {
    context.subscriptions.push(
        vscode.commands.registerCommand('rogueforge.buildAndDeploy', () => {
            const script = findScript();
            if (!script) {
                vscode.window.showErrorMessage('未找到 BuildAndDeploy.ps1，请打开包含该脚本的文件夹（美化城市）后重试。');
                return;
            }
            const terminal = vscode.window.createTerminal({ name: 'RogueForge 构建部署' });
            terminal.show(true);
            terminal.sendText(`powershell -NoProfile -ExecutionPolicy Bypass -File "${script}" -NoPause`);
        })
    );
}

function deactivate() { }

module.exports = { activate, deactivate };
