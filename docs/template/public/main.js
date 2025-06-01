import WorkflowContainer from "./workflow.js"

export default {
    defaultTheme: 'light',
    iconLinks: [{
        icon: 'github',
        href: 'https://github.com/bonsai-rx/spinnaker',
        title: 'GitHub'
    }],
    start: () => {
        WorkflowContainer.init();
    }
}
