<p align="center">
  <img src="./assets/git-shadowtree-wordmark.svg" alt="Logo" width="800">
</p>

Git Shadowtree is a tool that allows you to maintain a separate "shadow" repository alongside your main Git repository. The shadow repository can be used to track specific files and directories that you want to keep separate from the main repository, while still being able to track them in the shadow repository.

## Installation

## Scoop
```
scoop install https://raw.githubusercontent.com/szv/git-shadowtree/refs/heads/main/scoop/git-shadowtree.json
```

### WinGet
```
winget install Szv.GitShadowtree
```
Open a new terminal afterwards so the updated PATH takes effect, then use `git shadowtree …`.

### Manual
1. Download the latest release of Git Shadowtree from the [releases page](https://github.com/szv/git-shadowtree/releases).
2. Extract the downloaded archive to a directory of your choice.
3. Add the directory containing the `git-shadowtree` executable to your system's PATH environment variable.  
Open a new terminal afterwards so the updated PATH takes effect, then use `git shadowtree …`.

## Project Setup

Clone the remote shadow repository into your working repository:
```
git shadowtree clone <RemoteGitUrl>
```

Configure your user name and email for the shadow repository:
```
git shadowtree config user.name "..."
git shadowtree config user.email "..."
```

## Usage

The `.shadowtree` file contains patterns that specify which files and directories should be included in the shadow repository. You can edit this file to customize the contents of the shadow repository.
Files matched by the patterns in the `.shadowtree` file will be included in the shadow repository and excluded from the working repository. This allows you to keep certain files and directories separate from the main repository while still being able to track them in the shadow repository.

### Adding, Committing, and Pushing Changes
To add files to the shadow repository, simply add patterns to the `.shadowtree` file. For example, to include all files in the `.agents` directory, you can add the following pattern:
```
.agents/
```

Before committing changes to the shadow repository, you need to **stage** the changes using the `git shadowtree add` command. This command will add the specified files to the staging area of the shadow repository. For example, to stage all changes in the shadow repository, you can run:
```
git shadowtree add .
```

After staging the changes, you can **commit** them to the shadow repository using the `git shadowtree commit` command. This command will create a new commit in the shadow repository with the staged changes.
```
git shadowtree commit -m "Update shadow repository"
```
Committing changes to the shadow repository will also exclude the committed files from the working repository, ensuring that they are only tracked in the shadow repository.

To **push** the changes from the shadow repository to the remote repository, you can use the `git shadowtree push` command. This command will push the commits from the shadow repository to the specified remote repository.
```
git shadowtree push
```

### Pulling Changes
To **pull** changes from the remote shadow repository, you can use the `git shadowtree pull` command. This command will fetch the latest commits from the remote shadow repository and merge them into your local shadow repository. It will also update the exclusion of files in the working repository based on the updated shadow repository.
```
git shadowtree pull
```